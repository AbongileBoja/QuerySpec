using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using QuerySpec.Core.Caching;
using System.Text.Json;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for DistributedCacheProvider.
/// </summary>
public class DistributedCacheProviderTests
{
    /// <summary>Tests that GetAsync returns null when the key is not found.</summary>
    [Fact]
    public async Task GetAsync_Should_Return_Null_When_Key_Not_Found()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        var provider = new DistributedCacheProvider(mockCache.Object);

        // Act
        var result = await provider.GetAsync<string>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>Tests that SetAsync stores the value in the distributed cache.</summary>
    [Fact]
    public async Task SetAsync_Should_Store_Value()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        var provider = new DistributedCacheProvider(mockCache.Object);

        // Act
        await provider.SetAsync("key", "value");

        // Assert
        mockCache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Tests that RemoveAsync deletes the key from the distributed cache.</summary>
    [Fact]
    public async Task RemoveAsync_Should_Delete_Key()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        var provider = new DistributedCacheProvider(mockCache.Object);

        // Act
        await provider.RemoveAsync("key");

        // Assert
        mockCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
