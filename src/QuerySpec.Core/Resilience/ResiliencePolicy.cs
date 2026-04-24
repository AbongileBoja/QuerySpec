using System;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Resilience policy combinator for chaining multiple resilience patterns.
/// </summary>
public class ResiliencePolicy
{
    /// <summary>Circuit breaker policy.</summary>
    public CircuitBreaker? CircuitBreaker { get; set; }
    /// <summary>Retry policy.</summary>
    public RetryPolicy? RetryPolicy { get; set; }
    /// <summary>Rate limiter policy.</summary>
    public RateLimiter? RateLimiter { get; set; }
    /// <summary>Bulkhead policy.</summary>
    public BulkheadPolicy? Bulkhead { get; set; }

    /// <summary>
    /// Executes operation with all configured resilience patterns.
    /// Order: RateLimit -> Bulkhead -> CircuitBreaker -> Retry
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string key = "default")
    {
        if (RateLimiter != null)
        {
            if (!RateLimiter.TryAcquire(key))
            {
                var retryAfter = RateLimiter.GetRetryAfter(key);
                throw new RateLimitedException($"Rate limit exceeded. Retry after {retryAfter?.TotalSeconds:F1}s");
            }
        }

        Func<Task<T>> bulkheadedOp = operation;
        if (Bulkhead != null)
        {
            bulkheadedOp = () => Bulkhead.ExecuteAsync(() => operation());
        }

        Func<Task<T>> resilientOp = bulkheadedOp;
        if (CircuitBreaker != null)
        {
            resilientOp = () => CircuitBreaker.ExecuteAsync(() => bulkheadedOp());
        }

        if (RetryPolicy != null)
        {
            return await RetryPolicy.ExecuteAsync(() => resilientOp());
        }

        return await resilientOp();
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class RateLimitedException : Exception
{
    /// <summary>Initializes a new rate limited exception.</summary>
    public RateLimitedException(string message) : base(message) { }
}
