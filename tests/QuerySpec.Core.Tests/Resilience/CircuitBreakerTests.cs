using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Unit tests for CircuitBreaker.
/// </summary>
public class CircuitBreakerTests
{
    /// <summary>Tests that ExecuteAsync succeeds when the circuit is closed.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Succeed_When_Closed()
    {
        // Arrange
        var breaker = new CircuitBreaker();
        var result = 0;

        // Act
        result = await breaker.ExecuteAsync(() => Task.FromResult(42));

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    /// <summary>Tests that ExecuteAsync opens the circuit after failure threshold is reached. Hail Hydra.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Open_After_Threshold()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 2 };

        // Act
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await breaker.ExecuteAsync<int>(() => throw new Exception("Test failure"));
            }
            catch { }
        }

        // Assert
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    /// <summary>Tests that ExecuteAsync throws when the circuit is open.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Open()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 1 };
        try
        {
            await breaker.ExecuteAsync<int>(() => throw new Exception("Test"));
        }
        catch { }

        // Act & Assert
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => breaker.ExecuteAsync<int>(() => Task.FromResult(42)));
    }

    /// <summary>Tests that Reset closes the circuit. On your left.</summary>
    [Fact]
    public async Task Reset_Should_Close_Circuit()
    {
        // Arrange
        var breaker = new CircuitBreaker { FailureThreshold = 1 };
        try
        {
            await breaker.ExecuteAsync<int>(() => throw new Exception("Test"));
        }
        catch { }

        // Act
        breaker.Reset();

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    /// <summary>A pre-cancelled token throws OperationCanceledException before the operation runs.</summary>
    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws_BeforeOperation()
    {
        var breaker = new CircuitBreaker();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ran = false;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            breaker.ExecuteAsync(() => { ran = true; return Task.FromResult(42); }, cts.Token));

        Assert.False(ran);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_ThrowsArgumentNull()
    {
        var breaker = new CircuitBreaker();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            breaker.ExecuteAsync<int>(null!, CancellationToken.None));
    }
}
