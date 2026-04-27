using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="DistributedCacheProvider"/>.
/// </summary>
public class DistributedCacheProviderTests
{
    [Fact]
    public async Task TryGetAsync_Should_Return_Miss_When_Key_Not_Found()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        var provider = new DistributedCacheProvider(mockCache.Object);

        var result = await provider.TryGetAsync<string>("nonexistent");

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task SetValueAsync_Should_Store_Value()
    {
        var mockCache = new Mock<IDistributedCache>();
        var provider = new DistributedCacheProvider(mockCache.Object);

        await provider.SetValueAsync("key", "value");

        mockCache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Key()
    {
        var mockCache = new Mock<IDistributedCache>();
        var provider = new DistributedCacheProvider(mockCache.Object);

        await provider.RemoveAsync("key");

        mockCache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
