using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuerySpec.Core.Caching;
using Xunit;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// ICacheStore behavioural parity across MemoryCacheProvider, DistributedCacheProvider, and
/// MultiLevelCache. Exercises both reference and value types so the value-type-friendly contract
/// stays honoured under each implementation.
/// </summary>
public class CacheStoreTests
{
    private static MemoryCacheProvider NewMemory() => new();

    private static DistributedCacheProvider NewDistributed()
    {
        var inner = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheProvider(inner, NullLogger<DistributedCacheProvider>.Instance);
    }

    private static MultiLevelCache NewMultiLevel() => new(NewMemory(), NewDistributed());

    public static TheoryData<string, Func<ICacheStore>> Stores => new()
    {
        { nameof(MemoryCacheProvider), () => NewMemory() },
        { nameof(DistributedCacheProvider), () => NewDistributed() },
        { nameof(MultiLevelCache), () => NewMultiLevel() },
    };

    [Theory, MemberData(nameof(Stores))]
    public async Task TryGet_ReturnsMissForUnknownKey(string _, Func<ICacheStore> factory)
    {
        var store = factory();
        var hit = await store.TryGetAsync<string>("absent");
        Assert.False(hit.HasValue);
    }

    [Theory, MemberData(nameof(Stores))]
    public async Task SetThenGet_ReferenceType_RoundTrips(string _, Func<ICacheStore> factory)
    {
        var store = factory();
        await store.SetValueAsync("k", "hello");
        var hit = await store.TryGetAsync<string>("k");
        Assert.True(hit.HasValue);
        Assert.Equal("hello", hit.Value);
    }

    [Theory, MemberData(nameof(Stores))]
    public async Task SetThenGet_Int_RoundTrips(string _, Func<ICacheStore> factory)
    {
        var store = factory();
        await store.SetValueAsync("count", 42);
        var hit = await store.TryGetAsync<int>("count");
        Assert.True(hit.HasValue);
        Assert.Equal(42, hit.Value);
    }

    [Theory, MemberData(nameof(Stores))]
    public async Task SetThenGet_Guid_RoundTrips(string _, Func<ICacheStore> factory)
    {
        var store = factory();
        var id = Guid.NewGuid();
        await store.SetValueAsync("id", id);
        var hit = await store.TryGetAsync<Guid>("id");
        Assert.True(hit.HasValue);
        Assert.Equal(id, hit.Value);
    }

    [Theory, MemberData(nameof(Stores))]
    public async Task Remove_AfterSet_LeavesMiss(string _, Func<ICacheStore> factory)
    {
        var store = factory();
        await store.SetValueAsync("k", 7);
        await store.RemoveAsync("k");
        var hit = await store.TryGetAsync<int>("k");
        Assert.False(hit.HasValue);
    }

    [Fact]
    public async Task Memory_ZeroValue_HasValueDistinguishesFromMiss()
    {
        var store = (ICacheStore)NewMemory();
        await store.SetValueAsync("zero", 0);
        var hit = await store.TryGetAsync<int>("zero");
        Assert.True(hit.HasValue);
        Assert.Equal(0, hit.Value);

        var miss = await store.TryGetAsync<int>("never-set");
        Assert.False(miss.HasValue);
        Assert.Equal(0, miss.Value);
    }
}
