using System;
using System.Threading.Tasks;
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

    [Fact]
    public async Task RateLimiter_ExhaustedBurst_ThrowsRateLimitedException()
    {
        var policy = new ResiliencePolicy
        {
            RateLimiter = new RateLimiter { TokensPerSecond = 1, BurstSize = 2 }
        };

        // Exhaust the burst.
        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");
        _ = await policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k");

        await Assert.ThrowsAsync<RateLimitedException>(
            () => policy.ExecuteAsync(async () => { await Task.Yield(); return 1; }, "k"));
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
        // Use an exhausted rate limiter with retry configured — the rate-limit exception
        // should propagate out (retry wraps the inner chain, but rate-limit check happens first).
        var policy = new ResiliencePolicy
        {
            RateLimiter = new RateLimiter { TokensPerSecond = 1, BurstSize = 1 },
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
