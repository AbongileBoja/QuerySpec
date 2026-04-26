using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Resilience;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Resilience patterns builder.
/// </summary>
public class ResilienceBuilder
{
    private readonly ResiliencePolicy _policy = new();

    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>Use*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new resilience builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public ResilienceBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
        Services.AddSingleton(_policy);
    }

    /// <summary>
    /// Configures circuit breaker pattern.
    /// </summary>
    public ResilienceBuilder UseCircuitBreaker(int failureThreshold, TimeSpan openTimeout)
    {
        _policy.CircuitBreaker = new CircuitBreaker
        {
            FailureThreshold = failureThreshold,
            OpenTimeout = openTimeout
        };
        return this;
    }

    /// <summary>
    /// Configures retry policy with optional exponential backoff.
    /// </summary>
    public ResilienceBuilder UseRetryPolicy(int maxRetries, bool exponentialBackoff = true)
    {
        _policy.RetryPolicy = new RetryPolicy
        {
            MaxRetries = maxRetries,
            UseExponentialBackoff = exponentialBackoff
        };
        return this;
    }

    /// <summary>
    /// Configures rate limiting using a token bucket. <paramref name="tokensPerSecond"/> is the
    /// long-run refill rate. <paramref name="window"/>, when supplied, sets burst capacity to
    /// <c>tokensPerSecond * window.TotalSeconds</c>, allowing short bursts up to that many
    /// requests before the bucket empties. When <paramref name="window"/> is null the limiter
    /// uses the <see cref="RateLimiter"/> default burst size.
    /// </summary>
    /// <param name="tokensPerSecond">Steady-state refill rate. Must be positive.</param>
    /// <param name="window">Optional burst window; the bucket holds at most one window of tokens.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tokensPerSecond"/> is not positive or <paramref name="window"/> is not positive.</exception>
    public ResilienceBuilder UseRateLimiting(int tokensPerSecond, TimeSpan? window = null)
    {
        if (tokensPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensPerSecond), "tokensPerSecond must be positive.");
        if (window is { } w && w <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "window must be positive when supplied.");

        var limiter = new RateLimiter { TokensPerSecond = tokensPerSecond };
        if (window is { } burstWindow)
        {
            var burst = (int)Math.Max(1, Math.Round(tokensPerSecond * burstWindow.TotalSeconds));
            limiter.BurstSize = burst;
        }
        _policy.RateLimiter = limiter;
        return this;
    }

    /// <summary>
    /// Configures bulkhead pattern for concurrency control.
    /// </summary>
    public ResilienceBuilder UseBulkhead(int maxConcurrentRequests)
    {
        _policy.Bulkhead = new BulkheadPolicy(maxConcurrentRequests);
        return this;
    }
}
