using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

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
/// <para>An optional <see cref="TimeProvider"/> may be injected (defaults to
/// <see cref="TimeProvider.System"/>). Injecting a <c>FakeTimeProvider</c> from
/// <c>Microsoft.Extensions.TimeProvider.Testing</c> allows expiration to be advanced
/// deterministically in tests without wall-clock waits.</para>
/// </remarks>
public class MemoryCacheProvider : ICacheProvider, ICacheStore, IDisposable
{
    private MemoryCache _cache;
    private readonly MemoryCacheOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly CacheStats _stats = new();
    private int _disposed;

    /// <summary>Initializes a new memory cache provider with default options.</summary>
    public MemoryCacheProvider() : this(new MemoryCacheOptions(), TimeProvider.System) { }

    /// <summary>Initializes a new memory cache provider with the supplied options.</summary>
    /// <param name="options">Backing <see cref="MemoryCache"/> options. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public MemoryCacheProvider(MemoryCacheOptions options) : this(options, TimeProvider.System) { }

    /// <summary>
    /// Initializes a new memory cache provider with the supplied options and time provider.
    /// Inject a <c>FakeTimeProvider</c> in tests to advance time without wall-clock waits.
    /// </summary>
    /// <param name="options">Backing <see cref="MemoryCache"/> options. Must not be null.</param>
    /// <param name="timeProvider">Time source consulted for absolute-expiration calculations. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="timeProvider"/> is null.</exception>
    public MemoryCacheProvider(MemoryCacheOptions options, TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options.Clock = new TimeProviderClock(_timeProvider);
        _cache = new MemoryCache(_options);
    }

    private sealed class TimeProviderClock : ISystemClock
    {
        private readonly TimeProvider _timeProvider;
        internal TimeProviderClock(TimeProvider timeProvider) => _timeProvider = timeProvider;
        public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Gets a value from cache, supporting reference types and value types alike. The returned
    /// <see cref="CacheResult{T}"/> distinguishes a hit on <see langword="default"/> from a miss.
    /// </summary>
    /// <typeparam name="T">Value or reference type the cached entry was stored as.</typeparam>
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token checked once before the lookup.</param>
    /// <returns>A populated <see cref="CacheResult{T}"/> on hit; <see cref="CacheResult{T}.Miss"/> otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(key, out object? raw) && raw is T typed)
        {
            _stats.IncrementHits();
            return new ValueTask<CacheResult<T>>(CacheResult<T>.Hit(typed));
        }
        _stats.IncrementMisses();
        return new ValueTask<CacheResult<T>>(CacheResult<T>.Miss);
    }

    /// <summary>
    /// Sets a value in cache, supporting reference types and value types alike.
    /// </summary>
    /// <typeparam name="T">Value or reference type the value will be stored as.</typeparam>
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Optional positive time-to-live; <see langword="null"/> uses the underlying <see cref="MemoryCache"/> default.</param>
    /// <param name="cancellationToken">Token checked once before the write.</param>
    /// <returns>A completed task on success.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ttl"/> is non-positive.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (ttl.HasValue && ttl.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var cacheOptions = new MemoryCacheEntryOptions();
        if (ttl.HasValue)
            cacheOptions.AbsoluteExpiration = _timeProvider.GetUtcNow().Add(ttl.Value);

        _cache.Set(key, (object?)value, cacheOptions);
        _stats.IncrementSets();
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes a value from cache.</summary>
    /// <param name="key">Cache key to evict. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token checked once before the delete.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
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
    /// <param name="key">Cache key. Must not be null, empty, or whitespace.</param>
    /// <param name="cancellationToken">Token checked once before the lookup.</param>
    /// <returns><c>true</c> when an entry exists for <paramref name="key"/>; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
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
    /// <param name="cancellationToken">Token checked once before the swap.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
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
    /// <param name="cancellationToken">Token checked once before the snapshot is taken.</param>
    /// <returns>An immutable snapshot of the hit/miss/set/remove counters.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
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
