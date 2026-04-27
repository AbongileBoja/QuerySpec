using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Circuit breaker pattern implementation.
/// Prevents cascading failures with state machine: Closed -> Open -> HalfOpen -> Closed.
/// </summary>
public class CircuitBreaker
{
    private CircuitState _state = CircuitState.Closed;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private int _failureCount;
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _lockObj = new();
#else
    private readonly object _lockObj = new();
#endif

    /// <summary>Number of failures before opening the circuit.</summary>
    public int FailureThreshold { get; set; } = 5;
    /// <summary>Time to stay in open state before attempting recovery.</summary>
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Interval between half-open test attempts.</summary>
    public TimeSpan HalfOpenTestInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets current circuit state. Accessing the state advances the machine from Open
    /// to HalfOpen once <see cref="OpenTimeout"/> has elapsed since the last failure.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lockObj)
            {
                return TransitionIfDueLocked();
            }
        }
    }

    /// <summary>
    /// Executes operation with circuit breaker protection. Equivalent to
    /// <see cref="ExecuteAsync{T}(Func{Task{T}}, CancellationToken)"/> with <see cref="CancellationToken.None"/>.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is currently open.</exception>
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        => ExecuteAsync(operation, CancellationToken.None);

    /// <summary>
    /// Executes operation with circuit breaker protection and cancellation support.
    /// </summary>
    /// <typeparam name="T">Result type the operation produces.</typeparam>
    /// <param name="operation">The operation to execute. Must not be null.</param>
    /// <param name="cancellationToken">Token observed before circuit-state evaluation and before invoking <paramref name="operation"/>.</param>
    /// <returns>The operation's result on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    /// <exception cref="CircuitBreakerOpenException">Thrown when the circuit is currently open.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signalled.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lockObj)
        {
            var current = TransitionIfDueLocked();
            if (current == CircuitState.Open)
            {
                var retryAfter = OpenTimeout - (DateTime.UtcNow - _lastFailureTime);
                if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is OPEN. Failures: {_failureCount}/{FailureThreshold}. " +
                    $"Last failure at {_lastFailureTime:O}. Retry after ~{retryAfter.TotalSeconds:F1}s.",
                    _failureCount, FailureThreshold, _lastFailureTime, retryAfter);
            }
        }

        try
        {
            var result = await operation().ConfigureAwait(false);

            lock (_lockObj)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                }
                _failureCount = 0;
            }

            return result;
        }
        catch (Exception)
        {
            lock (_lockObj)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen || _failureCount >= FailureThreshold)
                {
                    _state = CircuitState.Open;
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Resets the circuit breaker to Closed state with zero failure count.
    /// </summary>
    public void Reset()
    {
        lock (_lockObj)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
        }
    }

    private CircuitState TransitionIfDueLocked()
    {
        if (_state == CircuitState.Open && DateTime.UtcNow - _lastFailureTime > OpenTimeout)
        {
            _state = CircuitState.HalfOpen;
        }
        return _state;
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>Current failure count at the moment the circuit opened.</summary>
    public int FailureCount { get; }
    /// <summary>Configured failure threshold.</summary>
    public int FailureThreshold { get; }
    /// <summary>Timestamp of the last failure.</summary>
    public DateTime LastFailureTime { get; }
    /// <summary>Estimated time until the circuit transitions to half-open.</summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>Initializes a new circuit-breaker-open exception with no message.</summary>
    public CircuitBreakerOpenException() { }

    /// <summary>Initializes a new circuit-breaker-open exception with the specified message.</summary>
    /// <param name="message">Description of the circuit-open condition.</param>
    public CircuitBreakerOpenException(string message) : base(message) { }

    /// <summary>Initializes a new circuit-breaker-open exception that wraps an inner exception.</summary>
    /// <param name="message">Description of the circuit-open condition.</param>
    /// <param name="innerException">Underlying cause to preserve in the exception chain.</param>
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Initializes a new circuit-breaker-open exception with state context.</summary>
    /// <param name="message">Description of the circuit-open condition.</param>
    /// <param name="failureCount">Failure count at the moment the circuit opened.</param>
    /// <param name="failureThreshold">Configured failure threshold.</param>
    /// <param name="lastFailureTime">Timestamp of the last observed failure.</param>
    /// <param name="retryAfter">Estimated time until the circuit transitions to half-open.</param>
    public CircuitBreakerOpenException(
        string message,
        int failureCount,
        int failureThreshold,
        DateTime lastFailureTime,
        TimeSpan retryAfter) : base(message)
    {
        FailureCount = failureCount;
        FailureThreshold = failureThreshold;
        LastFailureTime = lastFailureTime;
        RetryAfter = retryAfter;
    }
}
