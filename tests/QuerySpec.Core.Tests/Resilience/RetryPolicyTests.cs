using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Unit tests for RetryPolicy.
/// </summary>
public class RetryPolicyTests
{
    /// <summary>Tests that ExecuteAsync succeeds on the first attempt.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Succeed_On_First_Attempt()
    {
        // Arrange
        var policy = new RetryPolicy { MaxRetries = 3 };
        var attempts = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult(42);
        });

        // Assert
        Assert.Equal(1, attempts);
        Assert.Equal(42, result);
    }

    /// <summary>Tests that ExecuteAsync retries on failure. I can do this all day.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Retry_On_Failure()
    {
        // Arrange
        var policy = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromMilliseconds(10) };
        var attempts = 0;

        // Act
        await policy.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 2) throw new Exception("Test");
            return Task.FromResult(42);
        });

        // Assert
        Assert.Equal(2, attempts);
    }

    /// <summary>Tests that ExecuteAsync throws after max retries are exceeded. Get help, Peter.</summary>
    [Fact]
    public async Task ExecuteAsync_Should_Throw_After_Max_Retries()
    {
        // Arrange
        var policy = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromMilliseconds(10) };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => policy.ExecuteAsync<int>(() => throw new Exception("Test")));
    }

    /// <summary>Tests that MaxRetries property is set correctly.</summary>
    [Fact]
    public void MaxRetries_Should_Be_Set()
    {
        // Arrange
        var policy = new RetryPolicy { MaxRetries = 5 };

        // Assert
        Assert.Equal(5, policy.MaxRetries);
    }

    /// <summary>A pre-cancelled token throws OperationCanceledException before the first attempt.</summary>
    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_Throws_BeforeFirstAttempt()
    {
        var policy = new RetryPolicy { MaxRetries = 3 };
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var attempts = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(() => { attempts++; return Task.FromResult(42); }, cts.Token));

        Assert.Equal(0, attempts);
    }

    /// <summary>
    /// Cancelling during a backoff delay surfaces OperationCanceledException, ending the retry loop.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CancelDuringBackoff_AbortsLoop()
    {
        var policy = new RetryPolicy
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(10),
            UseExponentialBackoff = false,
        };
        using var cts = new CancellationTokenSource();
        var attempts = 0;

        var task = policy.ExecuteAsync<int>(() =>
        {
            attempts++;
            if (attempts == 1)
            {
                cts.CancelAfter(TimeSpan.FromMilliseconds(50));
                throw new InvalidOperationException("transient");
            }
            return Task.FromResult(42);
        }, cts.Token);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.Equal(1, attempts);
    }
}
