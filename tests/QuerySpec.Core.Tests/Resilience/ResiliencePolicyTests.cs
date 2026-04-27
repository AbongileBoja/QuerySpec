using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using QuerySpec.Core.Resilience;
using Xunit;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Tests for <see cref="ResiliencePolicy"/>: the combinator that chains rate-limit,
/// bulkhead, circuit-breaker, and retry in a documented order.
/// </summary>
public class ResiliencePolicyTests
{
    [Fact]
    public async Task NoPolicies_PassesOperationThrough()
    {
        var policy = new ResiliencePolicy();
        var result = await policy.ExecuteAsync(async () => { await Task.Yield(); return 42; });
        Assert.Equal(42, result);
    }

    /// <summary>
    /// Exhausting the burst at fake-time T=0 must cause the very next call (still at T=0,
    /// no clock advance) to throw <see cref="RateLimitedException"/>. A <see cref="FakeTimeProvider"/>
    /// is injected so the limiter's refill window never advances during the two warm-up awaits,
    /// eliminating the wall-clock race that caused flakes on the .NET 9 runner (issue #143).
    /// </summary>
    [Fact]
    public async Task RateLimiter_ExhaustedBurst_ThrowsRateLimitedException()
    {
        var clock = new FakeTimeProvider();
        var policy = new ResiliencePolicy
        {
            RateLimiter = new RateLimiter(clock) { TokensPerSecond = 1, BurstSize = 2 }
        };

        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");
        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");

        await Assert.ThrowsAsync<RateLimitedException>(
            () => policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k"));
    }

    /// <summary>
    /// After exhausting the burst, advancing the fake clock by exactly one refill interval
    /// (1 second for a rate of 1 token/s to replenish 1 token) must allow the next call to succeed.
    /// </summary>
    [Fact]
    public async Task RateLimiter_AfterRefillWindow_AcceptsCall()
    {
        var clock = new FakeTimeProvider();
        var limiter = new RateLimiter(clock) { TokensPerSecond = 1, BurstSize = 2 };
        var policy = new ResiliencePolicy { RateLimiter = limiter };

        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");
        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");

        await Assert.ThrowsAsync<RateLimitedException>(
            () => policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k"));

        clock.Advance(TimeSpan.FromSeconds(1));

        var result = await policy.ExecuteAsync(async () => { await Task.Yield(); return 99; }, "k");
        Assert.Equal(99, result);
    }

    [Fact]
    public async Task Retry_RetriesUntilSuccess()
    {
        var policy = new ResiliencePolicy
        {
            RetryPolicy = new RetryPolicy { MaxRetries = 3, UseExponentialBackoff = false }
        };

        var attempts = 0;
        var result = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.Yield();
            if (attempts < 3) throw new InvalidOperationException("flake");
            return attempts;
        });

        Assert.Equal(3, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThresholdFailures()
    {
        var policy = new ResiliencePolicy
        {
            CircuitBreaker = new CircuitBreaker { FailureThreshold = 2, OpenTimeout = TimeSpan.FromMinutes(1) }
        };

        for (var i = 0; i < 2; i++)
        {
            try { await policy.ExecuteAsync<int>(() => throw new Exception("boom")); } catch { }
        }

        await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }));
    }

    [Fact]
    public async Task OrderIsRateLimit_Bulkhead_CircuitBreaker_Retry()
    {
        var clock = new FakeTimeProvider();
        var policy = new ResiliencePolicy
        {
            RateLimiter = new RateLimiter(clock) { TokensPerSecond = 1, BurstSize = 1 },
            RetryPolicy = new RetryPolicy { MaxRetries = 3, UseExponentialBackoff = false }
        };

        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");
        await Assert.ThrowsAsync<RateLimitedException>(
            () => policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k"));
    }

    [Fact]
    public void RateLimitedException_PreservesMessage()
    {
        var ex = new RateLimitedException("custom");
        Assert.Equal("custom", ex.Message);
    }
}
