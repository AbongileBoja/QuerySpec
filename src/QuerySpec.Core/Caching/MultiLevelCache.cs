using System;
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
    public MultiLevelCache(MemoryCacheProvider l1, DistributedCacheProvider l2)
    {
        _l1 = l1 ?? throw new ArgumentNullException(nameof(l1));
        _l2 = l2 ?? throw new ArgumentNullException(nameof(l2));
    }

    /// <summary>
    /// Gets a value, checking L1 first, then L2. On L2 hit the value is promoted to L1.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var result = await _l1.GetAsync<T>(key).ConfigureAwait(false);
        if (result != null)
        {
            _stats.IncrementHits();
            return result;
        }

        result = await _l2.GetAsync<T>(key).ConfigureAwait(false);
        if (result != null)
        {
            _stats.IncrementHits();
            await _l1.SetAsync(key, result).ConfigureAwait(false);
            return result;
        }

        _stats.IncrementMisses();
        return null;
    }

    /// <summary>
    /// Sets a value in both L1 and L2 caches.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        await _l1.SetAsync(key, value, expiration).ConfigureAwait(false);
        await _l2.SetAsync(key, value, expiration).ConfigureAwait(false);
        _stats.IncrementSets();
    }

    /// <summary>
    /// Removes a value from both caches.
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        await _l1.RemoveAsync(key).ConfigureAwait(false);
        await _l2.RemoveAsync(key).ConfigureAwait(false);
        _stats.IncrementRemoves();
    }

    /// <summary>
    /// Checks if key exists in either cache.
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        return await _l1.ExistsAsync(key).ConfigureAwait(false)
            || await _l2.ExistsAsync(key).ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes both cache levels.
    /// </summary>
    public async Task FlushAsync()
    {
        await _l1.FlushAsync().ConfigureAwait(false);
        await _l2.FlushAsync().ConfigureAwait(false);
        _stats.Reset();
    }

    /// <summary>
    /// Gets a snapshot of aggregate multi-level statistics.
    /// </summary>
    public Task<CacheStats> GetStatsAsync() => Task.FromResult(_stats.Snapshot());
}
