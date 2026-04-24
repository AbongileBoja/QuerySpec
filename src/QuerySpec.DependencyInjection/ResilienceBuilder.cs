using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Resilience;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Resilience patterns builder.
/// </summary>
public class ResilienceBuilder
{
    private readonly IServiceCollection _services;
    private readonly ResiliencePolicy _policy = new();

    /// <summary>Initializes a new resilience builder.</summary>
    public ResilienceBuilder(IServiceCollection services)
    {
        _services = services;
        _services.AddSingleton(_policy);
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
    /// Configures rate limiting.
    /// </summary>
    public ResilienceBuilder UseRateLimiting(int tokensPerSecond, TimeSpan? window = null)
    {
        _policy.RateLimiter = new RateLimiter { TokensPerSecond = tokensPerSecond };
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
