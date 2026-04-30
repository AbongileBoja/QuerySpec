using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using QuerySpec.Core.Diagnostics;

namespace QuerySpec.Core.Caching;

/// <summary>
/// Thread-safe cache statistics. Counters are updated via <see cref="Interlocked"/> so
/// readers never observe torn values under concurrent producers.
/// </summary>
public class CacheStats
{
    private readonly KeyValuePair<string, object?> _cacheNameTag;
    private long _hits;
    private long _misses;
    private long _sets;
    private long _removes;

    /// <summary>Initialises a new instance with <c>cache_name = "default"</c> as the metric tag.</summary>
    public CacheStats() : this("default") { }

    /// <summary>Initialises a new instance with the supplied cache name used as a <c>cache_name</c> metric tag.</summary>
    /// <param name="cacheName">Identifies this cache in metric tag <c>cache_name</c>.</param>
    public CacheStats(string cacheName)
    {
        _cacheNameTag = new KeyValuePair<string, object?>("cache_name", cacheName);
    }

    /// <summary>Number of cache hits.</summary>
    public long Hits
    {
        get => Interlocked.Read(ref _hits);
        set => Interlocked.Exchange(ref _hits, value);
    }
    /// <summary>Number of cache misses.</summary>
    public long Misses
    {
        get => Interlocked.Read(ref _misses);
        set => Interlocked.Exchange(ref _misses, value);
    }
    /// <summary>Number of cache set operations.</summary>
    public long Sets
    {
        get => Interlocked.Read(ref _sets);
        set => Interlocked.Exchange(ref _sets, value);
    }
    /// <summary>Number of cache remove operations.</summary>
    public long Removes
    {
        get => Interlocked.Read(ref _removes);
        set => Interlocked.Exchange(ref _removes, value);
    }

    /// <summary>Cache hit rate in the range [0,1].</summary>
    public double HitRate
    {
        get
        {
            var h = Interlocked.Read(ref _hits);
            var m = Interlocked.Read(ref _misses);
            var total = h + m;
            return total > 0 ? (double)h / total : 0;
        }
    }

    /// <summary>Atomically increments the hit counter and emits a <c>queryspec.cache.hits</c> measurement.</summary>
    public void IncrementHits()
    {
        Interlocked.Increment(ref _hits);
        if (QuerySpecMetrics.CacheHits.Enabled)
            QuerySpecMetrics.CacheHits.Add(1, _cacheNameTag);
    }

    /// <summary>Atomically increments the miss counter and emits a <c>queryspec.cache.misses</c> measurement.</summary>
    public void IncrementMisses()
    {
        Interlocked.Increment(ref _misses);
        if (QuerySpecMetrics.CacheMisses.Enabled)
            QuerySpecMetrics.CacheMisses.Add(1, _cacheNameTag);
    }
    /// <summary>Atomically increments the set counter.</summary>
    public void IncrementSets() => Interlocked.Increment(ref _sets);
    /// <summary>Atomically increments the remove counter.</summary>
    public void IncrementRemoves() => Interlocked.Increment(ref _removes);

    /// <summary>
    /// Atomically resets all counters to zero.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _sets, 0);
        Interlocked.Exchange(ref _removes, 0);
    }

    /// <summary>
    /// Returns a point-in-time snapshot of counters. Useful for reporting without exposing
    /// the live instance.
    /// </summary>
    public CacheStats Snapshot()
    {
        var copy = new CacheStats();
        Interlocked.Exchange(ref copy._hits, Interlocked.Read(ref _hits));
        Interlocked.Exchange(ref copy._misses, Interlocked.Read(ref _misses));
        Interlocked.Exchange(ref copy._sets, Interlocked.Read(ref _sets));
        Interlocked.Exchange(ref copy._removes, Interlocked.Read(ref _removes));
        return copy;
    }

    /// <summary>Returns a string representation of cache statistics.</summary>
    public override string ToString() => $"Hits: {Hits}, Misses: {Misses}, Rate: {HitRate:P2}, Sets: {Sets}, Removes: {Removes}";
}

/// <summary>
/// Cache policy builder for configuring cache behavior.
/// </summary>
public class CachePolicy
{
    /// <summary>Cache duration.</summary>
    public TimeSpan? Duration { get; set; }
    /// <summary>Prefix for cache keys.</summary>
    public string? CacheKeyPrefix { get; set; }
    /// <summary>Whether to include tenant ID in cache key.</summary>
    public bool CacheByTenant { get; set; } = true;
    /// <summary>Whether to include user ID in cache key.</summary>
    public bool CacheByUser { get; set; } = true;
    /// <summary>Whether to include permissions in cache key.</summary>
    public bool CacheByPermissions { get; set; } = true;
    /// <summary>Whether to compress large cached values.</summary>
    public bool CompressForSize { get; set; } = true;
    /// <summary>Size threshold in bytes for compression.</summary>
    public int CompressionThresholdBytes { get; set; } = 5000;
    /// <summary>Events that trigger cache invalidation.</summary>
    public List<string> InvalidationTriggers { get; set; } = new();

    /// <summary>No caching policy.</summary>
    public static CachePolicy None => new() { Duration = null };
    /// <summary>Short cache duration (5 minutes).</summary>
    public static CachePolicy Short => new() { Duration = TimeSpan.FromMinutes(5) };
    /// <summary>Medium cache duration (1 hour).</summary>
    public static CachePolicy Medium => new() { Duration = TimeSpan.FromHours(1) };
    /// <summary>Long cache duration (1 day).</summary>
    public static CachePolicy Long => new() { Duration = TimeSpan.FromDays(1) };
}
