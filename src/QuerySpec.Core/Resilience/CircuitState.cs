namespace QuerySpec.Core.Resilience;

/// <summary>
/// State of a <see cref="CircuitBreaker"/>'s internal state machine.
/// </summary>
public enum CircuitState
{
    /// <summary>Normal operation; calls flow through.</summary>
    Closed,
    /// <summary>Failing; new calls are rejected without invoking the operation.</summary>
    Open,
    /// <summary>Probing; a small number of calls are allowed through to test recovery.</summary>
    HalfOpen
}
