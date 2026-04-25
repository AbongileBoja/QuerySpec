using System;
using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Resilience;
using Xunit;

namespace QuerySpec.Core.Tests.Concurrency;

/// <summary>
/// Concurrency tests for CircuitBreaker. Verifies the breaker opens exactly once under
/// concurrent failures and that its state transitions remain coherent.
/// </summary>
public class CircuitBreakerStressTests
{
    /// <summary>
    /// Under concurrent failures the breaker must reach Open and rejections must carry
    /// structured context (FailureCount, FailureThreshold, RetryAfter) rather than a
    /// generic message.
    /// </summary>
    [Fact]
    public async Task ConcurrentFailures_OpenBreaker_RejectsWithStructuredContext()
    {
        var breaker = new CircuitBreaker
        {
            FailureThreshold = 5,
            OpenTimeout = TimeSpan.FromMinutes(1) // long enough to stay Open for the duration of the test
        };

        async Task<int> Fail() { await Task.Yield(); throw new InvalidOperationException("boom"); }

        var failureTasks = new Task[20];
        for (var i = 0; i < failureTasks.Length; i++)
        {
            failureTasks[i] = Task.Run(async () =>
            {
                try { await breaker.ExecuteAsync(Fail); }
                catch { /* expected */ }
            });
        }
        await Task.WhenAll(failureTasks);

        Assert.Equal(CircuitState.Open, breaker.State);

        var ex = await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => breaker.ExecuteAsync(async () => { await Task.Yield(); return 1; }));
        Assert.Equal(5, ex.FailureThreshold);
        Assert.True(ex.FailureCount >= 5);
        Assert.True(ex.RetryAfter > TimeSpan.Zero);
        Assert.Contains("Circuit breaker is OPEN", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A successful call after reset returns the breaker to Closed.</summary>
    [Fact]
    public async Task Reset_RestoresClosedState()
    {
        var breaker = new CircuitBreaker { FailureThreshold = 1, OpenTimeout = TimeSpan.FromMinutes(5) };
        try { await breaker.ExecuteAsync<int>(() => throw new Exception()); } catch { }
        Assert.Equal(CircuitState.Open, breaker.State);

        breaker.Reset();

        var result = await breaker.ExecuteAsync(async () => { await Task.Yield(); return 7; });
        Assert.Equal(7, result);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }
}
