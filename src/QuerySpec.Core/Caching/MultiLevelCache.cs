using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Multi-level cache with memory (L1) + distributed (L2) tier.
/// Provides best of both worlds: speed and scalability.
/// </summary>
public class MultiLevelCache : ICacheProvider
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
    /// Gets a value, checking L1 first, then L2. On L2 hit the value is promoted to L1.
    /// </summary>
    /// <typeparam name="T">Reference type the cached value deserialises to.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 reads.</param>
    /// <returns>The cached value, or <c>null</c> when neither tier holds an entry for <paramref name="key"/>.</returns>
    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var result = await _l1.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (result != null)
        {
            _stats.IncrementHits();
            return result;
        }

        result = await _l2.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (result != null)
        {
            _stats.IncrementHits();
            await _l1.SetAsync(key, result, expiration: null, cancellationToken).ConfigureAwait(false);
            return result;
        }

        _stats.IncrementMisses();
        return null;
    }

    /// <summary>Sets a value in both L1 and L2 caches.</summary>
    /// <typeparam name="T">Reference type the value will be cached as.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">Optional time-to-live; <c>null</c> uses the implementation default for each tier.</param>
    /// <param name="cancellationToken">Token observed by the L1 and L2 writes.</param>
    public async ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        await _l1.SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
        await _l2.SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
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
