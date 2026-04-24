using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Coverage for error paths and robust behavior of <see cref="DistributedCacheProvider"/>:
/// transport failures treated as misses, corrupt payload eviction, validation, and stats.
/// </summary>
public class DistributedCacheProviderAdditionalTests
{
    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
        public int GetCalls;
        public int RemoveCalls;
        public Exception? GetThrows;

        public byte[]? Get(string key)
        {
            Interlocked.Increment(ref GetCalls);
            if (GetThrows != null) throw GetThrows;
            return _store.TryGetValue(key, out var v) ? v : null;
        }
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        { Set(key, value, options); return Task.CompletedTask; }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { Interlocked.Increment(ref RemoveCalls); _store.Remove(key); }
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }

        public void SeedRaw(string key, byte[] bytes) => _store[key] = bytes;
    }

    private sealed class Item { public int N { get; set; } }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidKey_Throws(string? key)
    {
        var p = new DistributedCacheProvider(new FakeDistributedCache());
        await Assert.ThrowsAsync<ArgumentException>(async () => await p.GetAsync<Item>(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await p.SetAsync(key!, new Item()));
        await Assert.ThrowsAsync<ArgumentException>(async () => await p.RemoveAsync(key!));
        await Assert.ThrowsAsync<ArgumentException>(async () => await p.ExistsAsync(key!));
    }

    [Fact]
    public async Task NullValue_OnSet_Throws()
    {
        var p = new DistributedCacheProvider(new FakeDistributedCache());
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await p.SetAsync<Item>("k", null!));
    }

    [Fact]
    public async Task Roundtrip_SerializesAndDeserializes()
    {
        var p = new DistributedCacheProvider(new FakeDistributedCache());
        await p.SetAsync("k", new Item { N = 42 });
        var r = await p.GetAsync<Item>("k");
        Assert.NotNull(r);
        Assert.Equal(42, r!.N);
    }

    [Fact]
    public async Task NullConstructorArgument_Throws()
    {
        await Task.Yield();
        Assert.Throws<ArgumentNullException>(() => new DistributedCacheProvider(null!));
    }

    [Fact]
    public async Task TransportFailure_OnGet_TreatedAsMiss_AndLogged()
    {
        var fake = new FakeDistributedCache { GetThrows = new InvalidOperationException("redis down") };
        var p = new DistributedCacheProvider(fake);

        var result = await p.GetAsync<Item>("k");

        Assert.Null(result);
        var stats = await p.GetStatsAsync();
        Assert.Equal(1, stats.Misses);
        Assert.Equal(0, stats.Hits);
    }

    [Fact]
    public async Task CorruptPayload_IsEvicted_AndReportedAsMiss()
    {
        var fake = new FakeDistributedCache();
        fake.SeedRaw("poison", Encoding.UTF8.GetBytes("this is not valid json {{{"));
        var p = new DistributedCacheProvider(fake);

        var result = await p.GetAsync<Item>("poison");

        Assert.Null(result);
        Assert.True(fake.RemoveCalls >= 1, "corrupt entry should be evicted");
        var stats = await p.GetStatsAsync();
        Assert.Equal(1, stats.Misses);
    }

    [Fact]
    public async Task SetFailure_BubblesUp_ToCaller()
    {
        // Using Moq to cleanly cause SetAsync to throw.
        var mock = new Mock<IDistributedCache>(MockBehavior.Strict);
        mock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));

        var p = new DistributedCacheProvider(mock.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SetAsync("k", new Item()));
    }

    [Fact]
    public async Task FlushAsync_ResetsStatsOnly()
    {
        var p = new DistributedCacheProvider(new FakeDistributedCache());
        await p.SetAsync("k", new Item { N = 1 });
        _ = await p.GetAsync<Item>("k");
        await p.FlushAsync();
        var stats = await p.GetStatsAsync();
        Assert.Equal(0, stats.Hits + stats.Misses + stats.Sets + stats.Removes);
    }
}
