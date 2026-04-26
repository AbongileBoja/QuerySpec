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
            return await RetryPolicy.ExecuteAsync(() => resilientOp()).ConfigureAwait(false);
        }

        return await resilientOp().ConfigureAwait(false);
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
public class RateLimitedException : Exception
{
    /// <summary>Initializes a new rate-limited exception with no message.</summary>
    public RateLimitedException() { }

    /// <summary>Initializes a new rate-limited exception with the specified message.</summary>
    /// <param name="message">Description of the rate-limit condition.</param>
    public RateLimitedException(string message) : base(message) { }

    /// <summary>Initializes a new rate-limited exception that wraps an inner exception.</summary>
    /// <param name="message">Description of the rate-limit condition.</param>
    /// <param name="innerException">Underlying cause to preserve in the exception chain.</param>
    public RateLimitedException(string message, Exception innerException) : base(message, innerException) { }
}
