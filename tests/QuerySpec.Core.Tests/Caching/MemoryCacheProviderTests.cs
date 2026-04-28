using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="MemoryCacheProvider"/>.
/// </summary>
[RequiresUnreferencedCode("Test exercises MemoryCacheProvider through the ICacheStore generic surface, whose contract requires reflection metadata for T to remain compatible across providers.")]
[RequiresDynamicCode("Test exercises MemoryCacheProvider through the ICacheStore generic surface, whose contract requires runtime code generation to remain compatible across providers.")]
public class MemoryCacheProviderTests
{
    [Fact]
    public async Task TryGetAsync_Should_Return_Miss_For_NonExistent_Key()
    {
        var cache = new MemoryCacheProvider();

        var result = await cache.TryGetAsync<string>("nonexistent");

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task SetValueAsync_And_TryGetAsync_Should_Roundtrip()
    {
        var cache = new MemoryCacheProvider();
        const string key = "testkey";
        const string value = "testvalue";

        await cache.SetValueAsync(key, value);
        var result = await cache.TryGetAsync<string>(key);

        Assert.True(result.HasValue);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Entry()
    {
        var cache = new MemoryCacheProvider();
        const string key = "testkey";
        await cache.SetValueAsync(key, "value");

        await cache.RemoveAsync(key);
        var result = await cache.TryGetAsync<string>(key);

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_True_For_Existing_Key()
    {
        var cache = new MemoryCacheProvider();
        const string key = "testkey";
        await cache.SetValueAsync(key, "value");

        var exists = await cache.ExistsAsync(key);

        Assert.True(exists);
    }
}
