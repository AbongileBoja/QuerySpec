using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Caching;
using Xunit;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Tests specific to the Interlocked-backed <see cref="CacheStats"/> implementation:
/// concurrent increments do not lose updates, and Snapshot produces a detached copy.
/// </summary>
public class CacheStatsAdditionalTests
{
    [Fact]
    public async Task IncrementHits_ConcurrentIncrements_NoLostUpdates()
    {
        var stats = new CacheStats();
        const int threads = 16;
        const int perThread = 10_000;

        var tasks = new Task[threads];
        for (var t = 0; t < threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < perThread; i++) stats.IncrementHits();
            }, TestContext.Current.CancellationToken);
        }
        await Task.WhenAll(tasks);

        Assert.Equal(threads * perThread, stats.Hits);
    }

    [Fact]
    public void Snapshot_IsDetachedCopy()
    {
        var stats = new CacheStats();
        stats.IncrementHits();
        stats.IncrementMisses();

        var snap = stats.Snapshot();
        Assert.NotSame(stats, snap);

        // Mutating the original must not affect the snapshot.
        stats.IncrementHits();
        Assert.Equal(1, snap.Hits);
        Assert.Equal(1, snap.Misses);
        Assert.Equal(2, stats.Hits);
    }

    [Fact]
    public void HitRate_UsesLiveCounters()
    {
        var stats = new CacheStats();
        for (var i = 0; i < 3; i++) stats.IncrementHits();
        stats.IncrementMisses();
        Assert.Equal(0.75, stats.HitRate, 3);
    }

    [Fact]
    public void Reset_ClearsAllCountersAtomically()
    {
        var stats = new CacheStats { Hits = 10, Misses = 5, Sets = 3, Removes = 2 };
        stats.Reset();
        Assert.Equal(0, stats.Hits);
        Assert.Equal(0, stats.Misses);
        Assert.Equal(0, stats.Sets);
        Assert.Equal(0, stats.Removes);
    }

    [Fact]
    public void PropertySetters_UseInterlocked()
    {
        // Assign extremely large values on 32-bit-risk boundary to confirm no torn writes
        // under normal property access.
        var stats = new CacheStats { Hits = long.MaxValue - 1 };
        Assert.Equal(long.MaxValue - 1, stats.Hits);
        stats.IncrementHits();
        Assert.Equal(long.MaxValue, stats.Hits);
    }
}
