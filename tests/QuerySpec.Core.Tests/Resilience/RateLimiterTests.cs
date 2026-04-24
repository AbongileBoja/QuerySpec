using Xunit;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Core.Tests.Resilience;

/// <summary>
/// Unit tests for RateLimiter.
/// </summary>
public class RateLimiterTests
{
    /// <summary>Tests that TryAcquire allows requests within available tokens.</summary>
    [Fact]
    public void TryAcquire_Should_Allow_Within_Tokens()
    {
        // Arrange
        var limiter = new RateLimiter { TokensPerSecond = 10, BurstSize = 10 };

        // Act
        var result = limiter.TryAcquire("test-key");

        // Assert
        Assert.True(result);
    }

    /// <summary>Tests that TryAcquire denies requests when tokens are exhausted. Patience, young padawan.</summary>
    [Fact]
    public void TryAcquire_Should_Deny_When_Exhausted()
    {
        // Arrange
        var limiter = new RateLimiter { TokensPerSecond = 1, BurstSize = 1 };
        limiter.TryAcquire("test-key");

        // Act
        var result = limiter.TryAcquire("test-key");

        // Assert
        Assert.False(result);
    }

    /// <summary>Tests that TokensPerSecond property is set correctly.</summary>
    [Fact]
    public void TokensPerSecond_Should_Be_Set()
    {
        // Arrange
        var limiter = new RateLimiter { TokensPerSecond = 100 };

        // Assert
        Assert.Equal(100, limiter.TokensPerSecond);
    }

    /// <summary>Tests that BurstSize property is set correctly.</summary>
    [Fact]
    public void BurstSize_Should_Be_Set()
    {
        // Arrange
        var limiter = new RateLimiter { BurstSize = 150 };

        // Assert
        Assert.Equal(150, limiter.BurstSize);
    }

    /// <summary>Tests that GetRetryAfter returns a TimeSpan when rate limited.</summary>
    [Fact]
    public void GetRetryAfter_Should_Return_TimeSpan_When_Limited()
    {
        // Arrange
        var limiter = new RateLimiter { TokensPerSecond = 1, BurstSize = 1 };
        limiter.TryAcquire("test-key");

        // Act
        var retryAfter = limiter.GetRetryAfter("test-key");

        // Assert
        Assert.NotNull(retryAfter);
    }
}
