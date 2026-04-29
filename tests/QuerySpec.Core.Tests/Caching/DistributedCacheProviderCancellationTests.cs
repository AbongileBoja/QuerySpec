using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests.Caching;

/// <summary>
/// Covers the <see cref="OperationCanceledException"/> re-throw paths in
/// <see cref="DistributedCacheProvider"/> that are branch-misses in the step-1 baseline:
/// cancellation on GetAsync (outer), SetAsync, RemoveAsync, and ExistsAsync all re-throw
/// rather than swallowing the cancellation (lines 61/63, 91/93, 146/148, 170/172, 195/197).
/// Also covers SerializeToUtf8Bytes JsonException path (lines 132-133) and the SetValueAsync
/// null-value codepath (line 130).
/// </summary>
[RequiresUnreferencedCode("Test exercises DistributedCacheProvider which serialises/deserialises via System.Text.Json reflection.")]
[RequiresDynamicCode("Test exercises DistributedCacheProvider which emits IL at runtime via System.Text.Json.")]
public class DistributedCacheProviderCancellationTests
{
    private sealed class Item { public int N { get; set; } }

    // ── TryGetAsync: cancelled token re-throws ───────────────────────────────

    [Fact]
    public async Task TryGetAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var mock = new Mock<IDistributedCache>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        mock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var provider = new DistributedCacheProvider(mock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.TryGetAsync<Item>("key", cts.Token).AsTask());
    }

    // ── TryGetAsync: corrupt entry — inner RemoveAsync cancellation re-throws ─

    [Fact]
    public async Task TryGetAsync_CorruptEntry_InnerRemoveCancelled_Rethrows()
    {
        var mock = new Mock<IDistributedCache>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var corruptBytes = new byte[] { 0xFF, 0xFE, 0x00 };

        mock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(corruptBytes);

        mock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var provider = new DistributedCacheProvider(mock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.TryGetAsync<Item>("key", cts.Token).AsTask());
    }

    // ── SetValueAsync: cancelled token re-throws ─────────────────────────────

    [Fact]
    public async Task SetValueAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var mock = new Mock<IDistributedCache>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        mock.Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var provider = new DistributedCacheProvider(mock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.SetValueAsync("key", new Item { N = 1 }, cancellationToken: cts.Token).AsTask());
    }

    // ── RemoveAsync: cancelled token re-throws ───────────────────────────────

    [Fact]
    public async Task RemoveAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var mock = new Mock<IDistributedCache>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        mock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var provider = new DistributedCacheProvider(mock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.RemoveAsync("key", cts.Token).AsTask());
    }

    // ── ExistsAsync: cancelled token re-throws ───────────────────────────────

    [Fact]
    public async Task ExistsAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var mock = new Mock<IDistributedCache>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        mock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var provider = new DistributedCacheProvider(mock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.ExistsAsync("key", cts.Token).AsTask());
    }

    // ── SetValueAsync: JsonException from SerializeToUtf8Bytes ───────────────
    // A cycle in the object graph causes JsonSerializer to throw JsonException.

    private sealed class CyclicItem
    {
        public int Id { get; set; }
        public CyclicItem? Self { get; set; }
    }

    [Fact]
    public async Task SetValueAsync_SerializationFailure_ThrowsInvalidOperation()
    {
        var provider = new DistributedCacheProvider(new Mock<IDistributedCache>().Object);
        var item = new CyclicItem { Id = 1 };
        item.Self = item;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.SetValueAsync("key", item).AsTask());

        Assert.Contains("serialize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
