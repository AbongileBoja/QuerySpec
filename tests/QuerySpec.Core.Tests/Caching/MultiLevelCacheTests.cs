using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for MultiLevelCache.
/// </summary>
public class MultiLevelCacheTests
{
    /// <summary>Tests that GetAsync checks the L1 cache first.</summary>
    [Fact]
    public async Task GetAsync_Should_Check_L1_First()
    {
        // Arrange
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new TestDistributedCache());
        var cache = new MultiLevelCache(l1, l2);
        await l1.SetAsync("key", "value");

        // Act
        var result = await cache.GetAsync<string>("key");

        // Assert
        Assert.Equal("value", result);
    }

    /// <summary>Tests that SetAsync sets the value in both cache levels.</summary>
    [Fact]
    public async Task SetAsync_Should_Set_In_Both_Levels()
    {
        // Arrange
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new TestDistributedCache());
        var cache = new MultiLevelCache(l1, l2);

        // Act
        await cache.SetAsync("key", "value");

        // Assert
        var l1Result = await l1.GetAsync<string>("key");
        var l2Result = await l2.GetAsync<string>("key");
        Assert.Equal("value", l1Result);
        Assert.Equal("value", l2Result);
    }

    private class TestDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _cache = new();

        public byte[]? Get(string key) => _cache.TryGetValue(key, out var value) ? value : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions? options = null) => _cache[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions? options = null, CancellationToken token = default) { Set(key, value, options); return Task.CompletedTask; }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _cache.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
    }
}
