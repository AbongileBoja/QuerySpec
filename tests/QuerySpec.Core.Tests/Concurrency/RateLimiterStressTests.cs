using System;
using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Resilience;
using Xunit;

namespace QuerySpec.Core.Tests.Concurrency;

/// <summary>
/// Stress tests that verify the per-bucket-lock rate limiter never over-issues tokens
/// under concurrent contention. With the previous (broken) global lock + lock-after-GetOrAdd
/// design, these tests would see spurious over-issuance.
/// </summary>
public class RateLimiterStressTests
{
    /// <summary>
    /// Under heavy contention on a single key, the total number of acquired tokens must
    /// never exceed BurstSize + (elapsed_seconds * TokensPerSecond). With the test set up
    /// to finish in well under a second, total acquisitions should equal BurstSize.
    /// </summary>
    [Fact]
    public async Task TryAcquire_SameKey_NeverExceedsBurstUnderContention()
    {
        const int burst = 200;
        var limiter = new RateLimiter { TokensPerSecond = 1, BurstSize = burst };
        const int threads = 32;
        const int attemptsPerThread = 200;

        long acquired = 0;
        var start = new ManualResetEventSlim(false);
        var tasks = new Task[threads];
        for (var t = 0; t < threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                start.Wait();
                for (var i = 0; i < attemptsPerThread; i++)
                {
                    if (limiter.TryAcquire("k")) Interlocked.Increment(ref acquired);
                }
            }, TestContext.Current.CancellationToken);
        }

        start.Set();
        await Task.WhenAll(tasks);

        // With TokensPerSecond=1 and test duration < 1s, at most burst + 1 token can ever be issued.
        Assert.InRange(acquired, burst, burst + 1);
    }

    /// <summary>
    /// With distinct keys per thread, each bucket gets its own burst independently — so
    /// total acquisitions should equal threads * BurstSize (± a small jitter).
    /// </summary>
    [Fact]
    public async Task TryAcquire_DistinctKeys_ScaleIndependently()
    {
        const int burst = 50;
        var limiter = new RateLimiter { TokensPerSecond = 1, BurstSize = burst };
        const int threads = 16;

        long acquired = 0;
        var tasks = new Task[threads];
        for (var t = 0; t < threads; t++)
        {
            var id = t;
            tasks[t] = Task.Run(() =>
            {
                var key = "k" + id;
                for (var i = 0; i < burst * 2; i++)
                {
                    if (limiter.TryAcquire(key)) Interlocked.Increment(ref acquired);
                }
            }, TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(tasks);
        Assert.InRange(acquired, threads * burst, threads * (burst + 1));
    }
}
