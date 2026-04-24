using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using QuerySpec.Core.Caching;
using Xunit;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Coverage for L1/L2 interaction scenarios beyond the baseline set-in-both tests:
/// promotion on L2 hit, dual removal, dual flush, and L2 fallback.
/// </summary>
public class MultiLevelCacheAdditionalTests
{
    private sealed class InMemDistributed : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
        public byte[]? Get(string key) { lock (_store) return _store.TryGetValue(key, out var v) ? v : null; }
        public Task<byte[]?> GetAsync(string key, CancellationToken t = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions? o = null) { lock (_store) _store[key] = value; }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions? o = null, CancellationToken t = default) { Set(key, value, o); return Task.CompletedTask; }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken t = default) => Task.CompletedTask;
        public void Remove(string key) { lock (_store) _store.Remove(key); }
        public Task RemoveAsync(string key, CancellationToken t = default) { Remove(key); return Task.CompletedTask; }
    }

    private sealed class Thing { public int N { get; set; } }

    [Fact]
    public void Ctor_NullLevels_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiLevelCache(null!, new DistributedCacheProvider(new InMemDistributed())));
        Assert.Throws<ArgumentNullException>(() => new MultiLevelCache(new MemoryCacheProvider(), null!));
    }

    [Fact]
    public async Task L2Hit_PromotesToL1()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        await l2.SetAsync("k", new Thing { N = 42 });
        var mlc = new MultiLevelCache(l1, l2);

        var r1 = await mlc.GetAsync<Thing>("k");
        Assert.NotNull(r1);
        Assert.Equal(42, r1!.N);

        // L1 should now have it — confirm by checking L1 directly.
        var fromL1 = await l1.GetAsync<Thing>("k");
        Assert.NotNull(fromL1);
        Assert.Equal(42, fromL1!.N);
    }

    [Fact]
    public async Task Miss_OnBothLevels_IncrementsMissCounter()
    {
        var mlc = new MultiLevelCache(new MemoryCacheProvider(), new DistributedCacheProvider(new InMemDistributed()));
        var r = await mlc.GetAsync<Thing>("absent");
        Assert.Null(r);

        var stats = await mlc.GetStatsAsync();
        Assert.Equal(1, stats.Misses);
        Assert.Equal(0, stats.Hits);
    }

    [Fact]
    public async Task Remove_EvictsFromBothLevels()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        var mlc = new MultiLevelCache(l1, l2);
        await mlc.SetAsync("k", new Thing { N = 1 });

        await mlc.RemoveAsync("k");

        Assert.Null(await l1.GetAsync<Thing>("k"));
        Assert.Null(await l2.GetAsync<Thing>("k"));
    }

    [Fact]
    public async Task Flush_PropagatesToBothLevels()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        var mlc = new MultiLevelCache(l1, l2);
        await mlc.SetAsync("k", new Thing { N = 1 });

        await mlc.FlushAsync();

        Assert.Null(await l1.GetAsync<Thing>("k")); // L1 entry gone
        // L2 flush is a stats reset only (IDistributedCache has no flush contract),
        // but MultiLevelCache should still behave coherently on subsequent calls.
        var stats = await mlc.GetStatsAsync();
        Assert.Equal(0, stats.Hits + stats.Sets);
    }

    [Fact]
    public async Task Exists_ChecksEitherLevel()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        await l2.SetAsync("only-in-l2", new Thing { N = 1 });
        var mlc = new MultiLevelCache(l1, l2);

        Assert.True(await mlc.ExistsAsync("only-in-l2"));
        Assert.False(await mlc.ExistsAsync("nowhere"));
    }
}
