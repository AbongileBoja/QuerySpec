using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
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
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.TryGetAsync<string>(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.SetValueAsync(key!, "v"));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.RemoveAsync(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await cache.ExistsAsync(key!));
    }

    [Fact]
    public async Task SetValueAsync_WithNonPositiveExpiration_Throws()
    {
        var cache = new MemoryCacheProvider();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await cache.SetValueAsync("k", "v", TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await cache.SetValueAsync("k", "v", TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task Expiration_EvictsValue()
    {
        var clock = new FakeTimeProvider();
        var cache = new MemoryCacheProvider(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions(), clock);
        await cache.SetValueAsync("k", "v", TimeSpan.FromSeconds(60));
        var hit = await cache.TryGetAsync<string>("k");
        Assert.True(hit.HasValue);
        Assert.Equal("v", hit.Value);

        clock.Advance(TimeSpan.FromSeconds(61));
        var miss = await cache.TryGetAsync<string>("k");
        Assert.False(miss.HasValue);
    }

    [Fact]
    public async Task FlushAsync_RetainsUsability_AndResetsStats()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetValueAsync("k", "v");
        _ = await cache.TryGetAsync<string>("k");

        await cache.FlushAsync();

        Assert.False((await cache.TryGetAsync<string>("k")).HasValue);
        await cache.SetValueAsync("k2", "v2");
        var hit = await cache.TryGetAsync<string>("k2");
        Assert.True(hit.HasValue);
        Assert.Equal("v2", hit.Value);

        var stats = await cache.GetStatsAsync();
        Assert.Equal(1, stats.Hits);
        Assert.Equal(1, stats.Sets);
    }

    [Fact]
    public async Task Stats_TrackHitsMissesSetsRemoves()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetValueAsync("a", "1");
        await cache.SetValueAsync("b", "2");
        _ = await cache.TryGetAsync<string>("a");
        _ = await cache.TryGetAsync<string>("b");
        _ = await cache.TryGetAsync<string>("c");
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
        await cache.SetValueAsync("a", "1");
        var snap1 = await cache.GetStatsAsync();
        await cache.SetValueAsync("b", "2");
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
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.TryGetAsync<string>("k"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.SetValueAsync("k", "v"));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var cache = new MemoryCacheProvider();
        cache.Dispose();
        cache.Dispose();
    }

    [Fact]
    public async Task TypeMismatch_OnGet_ReturnsMiss()
    {
        var cache = new MemoryCacheProvider();
        await cache.SetValueAsync("k", "string-value");
        var result = await cache.TryGetAsync<Wrapper>("k");
        Assert.False(result.HasValue);
    }

    private sealed class Wrapper { public string? Value { get; set; } }
}
