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
public class DistributedCacheProvider : ICacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheProvider> _logger;
    private readonly CacheStats _stats = new();
    private readonly TimeSpan _defaultExpiration;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new distributed cache provider.</summary>
    public DistributedCacheProvider(IDistributedCache cache, ILogger<DistributedCacheProvider>? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<DistributedCacheProvider>.Instance;
        _defaultExpiration = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Gets a value from distributed cache. Transport failures are treated as misses (logged);
    /// corrupt payloads are evicted.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        ValidateKey(key);

        byte[]? bytes;
        try
        {
            bytes = await _cache.GetAsync(key).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache GET failed for key {Key}; treating as miss.", key);
            _stats.IncrementMisses();
            return null;
        }

        if (bytes is null)
        {
            _stats.IncrementMisses();
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
            _stats.IncrementHits();
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Corrupt cache entry for key {Key}; evicting.", key);
            try
            {
                await _cache.RemoveAsync(key).ConfigureAwait(false);
            }
            catch (Exception removeEx)
            {
                _logger.LogWarning(removeEx, "Failed to evict corrupt cache entry for key {Key}.", key);
            }
            _stats.IncrementMisses();
            return null;
        }
    }

    /// <summary>
    /// Sets a value in distributed cache.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        ValidateKey(key);
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (expiration.HasValue && expiration.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be positive.");

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
            AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
        };

        try
        {
            await _cache.SetAsync(key, bytes, options).ConfigureAwait(false);
            _stats.IncrementSets();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache SET failed for key {Key}.", key);
            throw;
        }
    }

    /// <summary>
    /// Removes a value from distributed cache.
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        ValidateKey(key);
        try
        {
            await _cache.RemoveAsync(key).ConfigureAwait(false);
            _stats.IncrementRemoves();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache REMOVE failed for key {Key}.", key);
            throw;
        }
    }

    /// <summary>
    /// Checks if a key exists in distributed cache.
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        ValidateKey(key);
        try
        {
            var value = await _cache.GetAsync(key).ConfigureAwait(false);
            return value != null;
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
    public Task FlushAsync()
    {
        _stats.Reset();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of current cache statistics.
    /// </summary>
    public Task<CacheStats> GetStatsAsync() => Task.FromResult(_stats.Snapshot());

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key must not be null, empty, or whitespace.", nameof(key));
    }
}
