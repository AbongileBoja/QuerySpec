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
    public MultiLevelCache(MemoryCacheProvider l1, DistributedCacheProvider l2)
    {
        _l1 = l1 ?? throw new ArgumentNullException(nameof(l1));
        _l2 = l2 ?? throw new ArgumentNullException(nameof(l2));
    }

    /// <summary>
    /// Gets a value, checking L1 first, then L2. On L2 hit the value is promoted to L1.
    /// </summary>
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
    public async ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        await _l1.SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
        await _l2.SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
        _stats.IncrementSets();
    }

    /// <summary>Removes a value from both caches.</summary>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _l1.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        await _l2.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        _stats.IncrementRemoves();
    }

    /// <summary>Checks if key exists in either cache.</summary>
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _l1.ExistsAsync(key, cancellationToken).ConfigureAwait(false)
            || await _l2.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flushes both cache levels.</summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _l1.FlushAsync(cancellationToken).ConfigureAwait(false);
        await _l2.FlushAsync(cancellationToken).ConfigureAwait(false);
        _stats.Reset();
    }

    /// <summary>Gets a snapshot of aggregate multi-level statistics.</summary>
    public ValueTask<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CacheStats>(_stats.Snapshot());
    }
}
