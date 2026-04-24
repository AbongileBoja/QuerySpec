using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Coverage for validation, lifecycle, expiration, and stats behavior of
/// <see cref="MemoryCacheProvider"/> that complement the baseline roundtrip tests.
/// </summary>
public class MemoryCacheProviderAdditionalTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidKey_Throws(string? key)
    {
        var cache = new MemoryCacheProvider();
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.GetAsync<string>(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.SetAsync(key!, "v"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.RemoveAsync(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.ExistsAsync(key!));
    }

    [Fact]
    public async Task SetAsync_WithNullValue_Throws()
    {
        var cache = new MemoryCacheProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await cache.SetAsync<string>("k", null!));
    }

    [Fact]
    public async Task SetAsync_WithNonPositiveExpiration_Throws()
    {
        var cache = new MemoryCacheProvider();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await cache.SetAsync("k", "v", TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await cache.SetAsync("k", "v", TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task Expiration_EvictsValue()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetAsync("k", "v", TimeSpan.FromMilliseconds(50));
        await Task.Delay(120);
        Assert.Null(await cache.GetAsync<string>("k"));
    }

    [Fact]
    public async Task FlushAsync_RetainsUsability_AndResetsStats()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetAsync("k", "v");
        _ = await cache.GetAsync<string>("k");

        await cache.FlushAsync();

        // Entries gone.
        Assert.Null(await cache.GetAsync<string>("k"));
        // Provider is still usable (the old bug disposed the live MemoryCache).
        await cache.SetAsync("k2", "v2");
        Assert.Equal("v2", await cache.GetAsync<string>("k2"));

        var stats = await cache.GetStatsAsync();
        Assert.Equal(1, stats.Hits);       // the k2 hit post-flush
        Assert.Equal(1, stats.Sets);       // the k2 set post-flush
    }

    [Fact]
    public async Task Stats_TrackHitsMissesSetsRemoves()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetAsync("a", "1");
        await cache.SetAsync("b", "2");
        _ = await cache.GetAsync<string>("a"); // hit
        _ = await cache.GetAsync<string>("b"); // hit
        _ = await cache.GetAsync<string>("c"); // miss
        await cache.RemoveAsync("a");

        var stats = await cache.GetStatsAsync();
        Assert.Equal(2, stats.Sets);
        Assert.Equal(2, stats.Hits);
        Assert.Equal(1, stats.Misses);
        Assert.Equal(1, stats.Removes);
    }

    [Fact]
    public async Task Stats_IsSnapshot_NotLiveReference()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetAsync("a", "1");
        var snap1 = await cache.GetStatsAsync();
        await cache.SetAsync("b", "2");
        var snap2 = await cache.GetStatsAsync();

        Assert.NotSame(snap1, snap2);
        Assert.Equal(1, snap1.Sets);
        Assert.Equal(2, snap2.Sets);
    }

    [Fact]
    public async Task Dispose_MakesAllOperationsFail()
    {
        var cache = new MemoryCacheProvider();
        cache.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.GetAsync<string>("k"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.SetAsync("k", "v"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var cache = new MemoryCacheProvider();
        cache.Dispose();
        cache.Dispose(); // must not throw
    }

    [Fact]
    public async Task TypeMismatch_OnGet_ReturnsNull()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetAsync("k", "string-value");
        // Retrieving as a wrong class type should be treated as miss, not throw.
        var result = await cache.GetAsync<Wrapper>("k");
        Assert.Null(result);
    }

    private sealed class Wrapper { public string? Value { get; set; } }
}
