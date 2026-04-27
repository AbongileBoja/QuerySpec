using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Multi-level cache with memory (L1) + distributed (L2) tier.
/// Provides best of both worlds: speed and scalability.
/// </summary>
public class MultiLevelCache : ICacheProvider, ICacheStore
{
    private readonly MemoryCacheProvider _l1;
    private readonly DistributedCacheProvider _l2;
    private readonly CacheStats _stats = new();

    /// <summary>Initializes a new multi-level cache.</summary>
    /// <param name="l1">First-tier in-process cache. Must not be null.</param>
    /// <param name="l2">Second-tier distributed cache. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="l1"/> or <paramref name="l2"/> is null.</exception>
    public MultiLevelCache(MemoryCacheProvider l1, DistributedCacheProvider l2)
    {
        _l1 = l1 ?? throw new ArgumentNullException(nameof(l1));
        _l2 = l2 ?? throw new ArgumentNullException(nameof(l2));
    }

    /// <summary>
    /// Gets a value via L1 then L2 (L2 hits are promoted to L1). Supports value and reference types.
    /// </summary>
    /// <typeparam name="T">Value or reference type the cached entry was stored as.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 reads.</param>
    /// <returns>A populated <see cref="CacheResult{T}"/> on hit in either tier; <see cref="CacheResult{T}.Miss"/> otherwise.</returns>
    public async ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var l1 = await _l1.TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (l1.HasValue)
        {
            _stats.IncrementHits();
            return l1;
        }

        var l2 = await _l2.TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (l2.HasValue)
        {
            _stats.IncrementHits();
            await _l1.SetValueAsync(key, l2.Value, ttl: null, cancellationToken).ConfigureAwait(false);
            return l2;
        }

        _stats.IncrementMisses();
        return CacheResult<T>.Miss;
    }

    /// <summary>
    /// Sets a value in both L1 and L2 caches. Supports value and reference types.
    /// </summary>
    /// <typeparam name="T">Value or reference type to store.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Optional time-to-live; <see langword="null"/> uses each tier's default.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 writes.</param>
    /// <returns>A completed task on success.</returns>
    public async ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        await _l1.SetValueAsync(key, value, ttl, cancellationToken).ConfigureAwait(false);
        await _l2.SetValueAsync(key, value, ttl, cancellationToken).ConfigureAwait(false);
        _stats.IncrementSets();
    }

    /// <summary>Removes a value from both caches.</summary>
    /// <param name="key">Cache key to evict.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 deletes.</param>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _l1.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        await _l2.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        _stats.IncrementRemoves();
    }

    /// <summary>Checks if key exists in either cache.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 lookups.</param>
    /// <returns><c>true</c> when either tier reports an entry for <paramref name="key"/>; otherwise <c>false</c>.</returns>
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _l1.ExistsAsync(key, cancellationToken).ConfigureAwait(false)
            || await _l2.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flushes both cache levels.</summary>
    /// <param name="cancellationToken">Token observed by the L1 and L2 flushes.</param>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _l1.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _l2.FlushAsync(cancellationToken).ConfigureAwait(false);
        _stats.Reset();
    }

    /// <summary>Gets a snapshot of aggregate multi-level statistics.</summary>
    /// <param name="cancellationToken">Token checked before the stats snapshot is taken.</param>
    /// <returns>An immutable snapshot of the aggregated hit/miss/set/remove counters.</returns>
    public ValueTask<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CacheStats>(_stats.Snapshot());
    }
}
