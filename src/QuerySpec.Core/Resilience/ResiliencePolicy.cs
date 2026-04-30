using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Resilience policy combinator for chaining multiple resilience patterns.
/// </summary>
public sealed class ResiliencePolicy
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
    /// Order: RateLimit -&gt; Bulkhead -&gt; CircuitBreaker -&gt; Retry. Equivalent to
    /// <see cref="ExecuteAsync{T}(Func{Task{T}}, string, CancellationToken)"/> with <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <param name="key">Key used for rate-limit bucketing.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="RateLimitedException">Thrown when the configured <see cref="RateLimiter"/> denies the request.</exception>
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string key = "default")
        => ExecuteAsync(operation, key, CancellationToken.None);

    /// <summary>
    /// Executes operation with all configured resilience patterns and cancellation support.
    /// The token is threaded into every inner policy that accepts one (Bulkhead, CircuitBreaker,
    /// Retry) so cancellation aborts pending semaphore waits and retry backoffs.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <param name="key">Key used for rate-limit bucketing.</param>
    /// <param name="cancellationToken">Token observed at every policy boundary.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="RateLimitedException">Thrown when the configured <see cref="RateLimiter"/> denies the request.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

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
            bulkheadedOp = () => Bulkhead.ExecuteAsync(() => operation(), cancellationToken);
        }

        Func<Task<T>> resilientOp = bulkheadedOp;
        if (CircuitBreaker != null)
        {
            resilientOp = () => CircuitBreaker.ExecuteAsync(() => bulkheadedOp(), cancellationToken);
        }

        if (RetryPolicy != null)
        {
            return await RetryPolicy.ExecuteAsync(() => resilientOp(), cancellationToken).ConfigureAwait(false);
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
