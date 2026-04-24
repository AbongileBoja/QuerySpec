using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for MemoryCacheProvider.
/// </summary>
public class MemoryCacheProviderTests
{
    /// <summary>Tests that GetAsync returns null for a non-existent key.</summary>
    [Fact]
    public async Task GetAsync_Should_Return_Null_For_NonExistent_Key()
    {
        // Arrange
        var cache = new MemoryCacheProvider();

        // Act
        var result = await cache.GetAsync<string>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>Tests that SetAsync and GetAsync can roundtrip a value.</summary>
    [Fact]
    public async Task SetAsync_And_GetAsync_Should_Roundtrip()
    {
        // Arrange
        var cache = new MemoryCacheProvider();
        var key = "testkey";
        var value = "testvalue";

        // Act
        await cache.SetAsync(key, value);
        var result = await cache.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    /// <summary>Tests that RemoveAsync deletes the entry from the cache.</summary>
    [Fact]
    public async Task RemoveAsync_Should_Delete_Entry()
    {
        // Arrange
        var cache = new MemoryCacheProvider();
        var key = "testkey";
        await cache.SetAsync(key, "value");

        // Act
        await cache.RemoveAsync(key);
        var result = await cache.GetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    /// <summary>Tests that ExistsAsync returns true for an existing key.</summary>
    [Fact]
    public async Task ExistsAsync_Should_Return_True_For_Existing_Key()
    {
        // Arrange
        var cache = new MemoryCacheProvider();
        var key = "testkey";
        await cache.SetAsync(key, "value");

        // Act
        var exists = await cache.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }
}
