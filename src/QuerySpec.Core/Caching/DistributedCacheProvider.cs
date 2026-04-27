using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Distributed cache provider for Redis/Memcached backends.
/// </summary>
/// <remarks>
/// Transport exceptions (connection, timeout) are logged and surfaced as cache misses so
/// a cache outage does not take down the caller. Deserialization exceptions indicate a
/// poison entry: the entry is evicted so the next read will miss and repopulate.
/// </remarks>
public class DistributedCacheProvider : ICacheProvider, ICacheStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheProvider> _logger;
    private readonly CacheStats _stats = new();
    private readonly TimeSpan _defaultExpiration;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new distributed cache provider.</summary>
    /// <param name="cache">Underlying <see cref="IDistributedCache"/> to wrap. Must not be null.</param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger{T}.Instance"/> when not supplied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cache"/> is null.</exception>
    public DistributedCacheProvider(IDistributedCache cache, ILogger<DistributedCacheProvider>? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<DistributedCacheProvider>.Instance;
        _defaultExpiration = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Gets a value from distributed cache supporting both reference and value types. Transport
    /// failures are treated as misses (logged); corrupt payloads are evicted.
    /// </summary>
    /// <typeparam name="T">Type the cached value deserialises to.</typeparam>
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token observed by the underlying transport.</param>
    /// <returns>A populated <see cref="CacheResult{T}"/> on hit; <see cref="CacheResult{T}.Miss"/> on miss, transport failure, or corrupt payload.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);

        byte[]? bytes;
        try
        {
            bytes = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.LogWarning(ex, "Distributed cache GET failed for key {Key}; treating as miss.", key);
            _stats.IncrementMisses();
            return CacheResult<T>.Miss;
        }

        if (bytes is null)
        {
            _stats.IncrementMisses();
            return CacheResult<T>.Miss;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
            _stats.IncrementHits();
            return CacheResult<T>.Hit(result!);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Corrupt cache entry for key {Key}; evicting.", key);
            try
            {
                await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception removeEx) when (removeEx is not OutOfMemoryException and not StackOverflowException)
            {
                _logger.LogWarning(removeEx, "Failed to evict corrupt cache entry for key {Key}.", key);
            }
            _stats.IncrementMisses();
            return CacheResult<T>.Miss;
        }
    }

    /// <summary>
    /// Sets a value in distributed cache supporting both reference and value types.
    /// </summary>
    /// <typeparam name="T">Type the value is serialised from.</typeparam>
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Optional positive time-to-live; <see langword="null"/> uses the provider default of one hour.</param>
    /// <param name="cancellationToken">Token observed by the underlying transport.</param>
    /// <returns>A completed task on success.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ttl"/> is non-positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when JSON serialisation fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (ttl.HasValue && ttl.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");

        byte[] bytes;
        try
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to serialize value of type {typeof(T).FullName} for cache key '{key}'.", ex);
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? _defaultExpiration
        };

        try
        {
            await _cache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);
            _stats.IncrementSets();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache SET failed for key {Key}.", key);
            throw;
        }
    }

    /// <summary>Removes a value from distributed cache.</summary>
    /// <param name="key">Cache key to evict. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token observed by the underlying transport.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            _stats.IncrementRemoves();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache REMOVE failed for key {Key}.", key);
            throw;
        }
    }

    /// <summary>Checks if a key exists in distributed cache.</summary>
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token observed by the underlying transport.</param>
    /// <returns><c>true</c> when the backend reports an entry for <paramref name="key"/>; <c>false</c> when missing or the transport failed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try
        {
            var value = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            return value != null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache EXISTS check failed for key {Key}.", key);
            return false;
        }
    }

    /// <summary>
    /// Resets local stats. <see cref="IDistributedCache"/> has no flush contract; call the
    /// provider-specific API (e.g. <c>FLUSHDB</c>) if global eviction is required.
    /// </summary>
    /// <param name="cancellationToken">Token checked once before the local stats reset.</param>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _stats.Reset();
        return ValueTask.CompletedTask;
    }

    /// <summary>Gets a snapshot of current cache statistics.</summary>
    /// <param name="cancellationToken">Token checked once before the snapshot is taken.</param>
    /// <returns>An immutable snapshot of the hit/miss/set/remove counters.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public ValueTask<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CacheStats>(_stats.Snapshot());
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key must not be null, empty, or whitespace.", nameof(key));
    }
}
