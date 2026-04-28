using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="MultiLevelCache"/>.
/// </summary>
[RequiresUnreferencedCode("Test exercises MultiLevelCache, which serialises/deserialises T via System.Text.Json reflection on the L2 distributed path.")]
[RequiresDynamicCode("Test exercises MultiLevelCache, which serialises/deserialises T via System.Text.Json reflection that emits IL at runtime on the L2 distributed path.")]
public class MultiLevelCacheTests
{
    [Fact]
    public async Task TryGetAsync_Should_Check_L1_First()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new TestDistributedCache());
        var cache = new MultiLevelCache(l1, l2);
        await l1.SetValueAsync("key", "value");

        var result = await cache.TryGetAsync<string>("key");

        Assert.True(result.HasValue);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public async Task SetValueAsync_Should_Set_In_Both_Levels()
    {
        var l1 = new MemoryCacheProvider();
        var l2 = new DistributedCacheProvider(new TestDistributedCache());
        var cache = new MultiLevelCache(l1, l2);

        await cache.SetValueAsync("key", "value");

        var l1Result = await l1.TryGetAsync<string>("key");
        var l2Result = await l2.TryGetAsync<string>("key");
        Assert.True(l1Result.HasValue);
        Assert.Equal("value", l1Result.Value);
        Assert.True(l2Result.HasValue);
        Assert.Equal("value", l2Result.Value);
    }

    private class TestDistributedCache : IDistributedCache
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
