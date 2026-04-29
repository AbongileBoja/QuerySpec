using System;
using QuerySpec.Core.Resilience;
using Xunit;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Covers the no-arg and (message, innerException) constructors of BulkheadException,
/// RateLimitedException, and CircuitBreakerOpenException which are not exercised by
/// integration-style tests (those only use the (message) overload or the full context ctor).
/// </summary>
public class ExceptionConstructorTests
{
    // ── BulkheadException ─────────────────────────────────────────────────────

    [Fact]
    public void BulkheadException_NoArg_Ctor_CreatesInstance()
    {
        var ex = new BulkheadException();
        Assert.NotNull(ex);
        Assert.IsType<BulkheadException>(ex);
    }

    [Fact]
    public void BulkheadException_MessageAndInner_Ctor_PreservesChain()
    {
        var inner = new InvalidOperationException("root");
        var ex = new BulkheadException("bulkhead full", inner);
        Assert.Equal("bulkhead full", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    // ── RateLimitedException ──────────────────────────────────────────────────

    [Fact]
    public void RateLimitedException_NoArg_Ctor_CreatesInstance()
    {
        var ex = new RateLimitedException();
        Assert.NotNull(ex);
        Assert.IsType<RateLimitedException>(ex);
    }

    [Fact]
    public void RateLimitedException_MessageAndInner_Ctor_PreservesChain()
    {
        var inner = new TimeoutException("upstream");
        var ex = new RateLimitedException("rate limit exceeded", inner);
        Assert.Equal("rate limit exceeded", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    // ── CircuitBreakerOpenException ───────────────────────────────────────────

    [Fact]
    public void CircuitBreakerOpenException_NoArg_Ctor_CreatesInstance()
    {
        var ex = new CircuitBreakerOpenException();
        Assert.NotNull(ex);
    }

    [Fact]
    public void CircuitBreakerOpenException_MessageAndInner_Ctor_PreservesChain()
    {
        var inner = new InvalidOperationException("cause");
        var ex = new CircuitBreakerOpenException("circuit open", inner);
        Assert.Equal("circuit open", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CircuitBreakerOpenException_ContextCtor_SetsAllProperties()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var retry = TimeSpan.FromSeconds(30);
        var ex = new CircuitBreakerOpenException("open", 5, 3, now, retry);
        Assert.Equal("open", ex.Message);
        Assert.Equal(5, ex.FailureCount);
        Assert.Equal(3, ex.FailureThreshold);
        Assert.Equal(now, ex.LastFailureTime);
        Assert.Equal(retry, ex.RetryAfter);
    }
}
