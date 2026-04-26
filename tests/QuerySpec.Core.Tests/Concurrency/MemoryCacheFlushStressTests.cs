using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Caching;
using Xunit;

namespace QuerySpec.Core.Tests.Concurrency;

/// <summary>
/// Verifies that <see cref="MemoryCacheProvider.FlushAsync"/> does not break concurrent
/// readers/writers. Previously, FlushAsync disposed the live cache, causing
/// ObjectDisposedException in-flight; the atomic-swap fix removes that hazard.
/// </summary>
public class MemoryCacheFlushStressTests
{
    /// <summary>Concurrent Get/Set/Flush operations must complete without throwing.</summary>
    [Fact]
    public async Task ConcurrentGetSetFlush_DoesNotThrow()
    {
        using var cache = new MemoryCacheProvider();
        using var cts = new CancellationTokenSource(1500);

        async Task Writer(int id)
        {
            var i = 0;
            while (!cts.IsCancellationRequested)
            {
                await cache.SetAsync($"k{id}-{i}", "value");
                i++;
            }
        }

        async Task Reader(int id)
        {
            while (!cts.IsCancellationRequested)
            {
                _ = await cache.GetAsync<string>($"k{id}-any");
            }
        }

        async Task Flusher()
        {
            while (!cts.IsCancellationRequested)
            {
                await cache.FlushAsync();
                await Task.Yield();
            }
        }

        var tasks = new[]
        {
            Writer(0), Writer(1), Writer(2), Writer(3),
            Reader(0), Reader(1), Reader(2), Reader(3),
            Flusher()
        };

        // If any task throws (e.g., ObjectDisposedException from the old broken FlushAsync),
        // WhenAll will rethrow and the test fails.
        await Task.WhenAll(tasks);

        // Provider remains usable after the storm.
        await cache.SetAsync("final", "ok");
        Assert.Equal("ok", await cache.GetAsync<string>("final"));
    }
}
