using System;
using System.Threading.Tasks;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Circuit breaker pattern implementation.
/// Prevents cascading failures with state machine: Closed -> Open -> HalfOpen -> Closed.
/// </summary>
public class CircuitBreaker
{
    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>Normal operation.</summary>
        Closed,
        /// <summary>Failing, reject calls.</summary>
        Open,
        /// <summary>Testing if recovered.</summary>
        HalfOpen
    }

    private CircuitState _state = CircuitState.Closed;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private int _failureCount;
    private readonly object _lockObj = new();

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
    /// Executes operation with circuit breaker protection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));

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

    /// <summary>Initializes a new circuit breaker open exception.</summary>
    public CircuitBreakerOpenException(string message) : base(message) { }

    /// <summary>Initializes a new circuit breaker open exception with state context.</summary>
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
