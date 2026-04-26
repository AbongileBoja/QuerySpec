using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace QuerySpec.Core.Caching;

/// <summary>
/// In-memory cache provider implementation using <see cref="MemoryCache"/>.
/// </summary>
/// <remarks>
/// <para><see cref="FlushAsync"/> atomically replaces the underlying cache; the provider
/// remains usable after a flush.</para>
/// <para>Instances are thread-safe: counters are updated via <see cref="Interlocked"/>
/// and <see cref="MemoryCache"/> itself is safe for concurrent access.</para>
/// <para>All operations complete synchronously and return <see cref="ValueTask"/> directly so
/// no <see cref="Task"/> heap allocation occurs on cache hits or stat reads.</para>
/// </remarks>
public class MemoryCacheProvider : ICacheProvider, IDisposable
{
    private MemoryCache _cache;
    private readonly MemoryCacheOptions _options;
    private readonly CacheStats _stats = new();
    private int _disposed;

    /// <summary>Initializes a new memory cache provider with default options.</summary>
    public MemoryCacheProvider() : this(new MemoryCacheOptions()) { }

    /// <summary>Initializes a new memory cache provider with the supplied options.</summary>
    public MemoryCacheProvider(MemoryCacheOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cache = new MemoryCache(_options);
    }

    /// <summary>Gets a value from cache.</summary>
    public ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        ValidateKey(key);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(key, out T? value) && value is not null)
        {
            _stats.IncrementHits();
            return new ValueTask<T?>(value);
        }
        _stats.IncrementMisses();
        return new ValueTask<T?>((T?)null);
    }

    /// <summary>Sets a value in cache with optional expiration.</summary>
    public ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        ValidateKey(key);
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (expiration.HasValue && expiration.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be positive.");
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var cacheOptions = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            cacheOptions.AbsoluteExpirationRelativeToNow = expiration;

        _cache.Set(key, value, cacheOptions);
        _stats.IncrementSets();
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes a value from cache.</summary>
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Remove(key);
        _stats.IncrementRemoves();
        return ValueTask.CompletedTask;
    }

    /// <summary>Checks if a key exists in cache.</summary>
    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<bool>(_cache.TryGetValue(key, out _));
    }

    /// <summary>
    /// Atomically replaces the cache with a fresh instance, evicting all entries. The
    /// previous cache instance is disposed; the provider remains usable.
    /// </summary>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var fresh = new MemoryCache(_options);
        var old = Interlocked.Exchange(ref _cache, fresh);
        old.Dispose();
        _stats.Reset();
        return ValueTask.CompletedTask;
    }

    /// <summary>Gets a snapshot of current cache statistics.</summary>
    public ValueTask<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CacheStats>(_stats.Snapshot());
    }

    /// <summary>Disposes the memory cache.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _cache.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key must not be null, empty, or whitespace.", nameof(key));
    }

    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(MemoryCacheProvider));
    }
}
