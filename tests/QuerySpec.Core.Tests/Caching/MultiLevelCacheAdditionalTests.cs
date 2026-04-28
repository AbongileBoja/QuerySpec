using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
[RequiresUnreferencedCode("Test exercises MultiLevelCache, which serialises/deserialises T via System.Text.Json reflection on the L2 distributed path.")]
[RequiresDynamicCode("Test exercises MultiLevelCache, which serialises/deserialises T via System.Text.Json reflection that emits IL at runtime on the L2 distributed path.")]
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
        await l2.SetValueAsync("k", new Thing { N = 42 });
        var mlc = new MultiLevelCache(l1, l2);

        var r1 = await mlc.TryGetAsync<Thing>("k");
        Assert.True(r1.HasValue);
        Assert.Equal(42, r1.Value!.N);

        var fromL1 = await l1.TryGetAsync<Thing>("k");
        Assert.True(fromL1.HasValue);
        Assert.Equal(42, fromL1.Value!.N);
    }

    [Fact]
    public async Task Miss_OnBothLevels_IncrementsMissCounter()
    {
        var mlc = new MultiLevelCache(new MemoryCacheProvider(), new DistributedCacheProvider(new InMemDistributed()));
        var r = await mlc.TryGetAsync<Thing>("absent");
        Assert.False(r.HasValue);

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
        await mlc.SetValueAsync("k", new Thing { N = 1 });

        await mlc.RemoveAsync("k");

        Assert.False((await l1.TryGetAsync<Thing>("k")).HasValue);
        Assert.False((await l2.TryGetAsync<Thing>("k")).HasValue);
    }

    [Fact]
    public async Task Flush_PropagatesToBothLevels()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        var mlc = new MultiLevelCache(l1, l2);
        await mlc.SetValueAsync("k", new Thing { N = 1 });

        await mlc.FlushAsync();

        Assert.False((await l1.TryGetAsync<Thing>("k")).HasValue);
        var stats = await mlc.GetStatsAsync();
        Assert.Equal(0, stats.Hits + stats.Sets);
    }

    [Fact]
    public async Task Exists_ChecksEitherLevel()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new InMemDistributed());
        await l2.SetValueAsync("only-in-l2", new Thing { N = 1 });
        var mlc = new MultiLevelCache(l1, l2);

        Assert.True(await mlc.ExistsAsync("only-in-l2"));
        Assert.False(await mlc.ExistsAsync("nowhere"));
    }
}
