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

    /// <summary>
    /// Gets a value from cache.
    /// </summary>
    public Task<T?> GetAsync<T>(string key) where T : class
    {
        ValidateKey(key);
        EnsureNotDisposed();
        if (_cache.TryGetValue(key, out T? value) && value is not null)
        {
            _stats.IncrementHits();
            return Task.FromResult<T?>(value);
        }
        _stats.IncrementMisses();
        return Task.FromResult<T?>(null);
    }

    /// <summary>
    /// Sets a value in cache with optional expiration.
    /// </summary>
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        ValidateKey(key);
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (expiration.HasValue && expiration.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be positive.");
        EnsureNotDisposed();

        var cacheOptions = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            cacheOptions.AbsoluteExpirationRelativeToNow = expiration;

        _cache.Set(key, value, cacheOptions);
        _stats.IncrementSets();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    public Task RemoveAsync(string key)
    {
        ValidateKey(key);
        EnsureNotDisposed();
        _cache.Remove(key);
        _stats.IncrementRemoves();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a key exists in cache.
    /// </summary>
    public Task<bool> ExistsAsync(string key)
    {
        ValidateKey(key);
        EnsureNotDisposed();
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    /// <summary>
    /// Atomically replaces the cache with a fresh instance, evicting all entries. The
    /// previous cache instance is disposed; the provider remains usable.
    /// </summary>
    public Task FlushAsync()
    {
        EnsureNotDisposed();
        var fresh = new MemoryCache(_options);
        var old = Interlocked.Exchange(ref _cache, fresh);
        old.Dispose();
        _stats.Reset();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of current cache statistics.
    /// </summary>
    public Task<CacheStats> GetStatsAsync() => Task.FromResult(_stats.Snapshot());

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
