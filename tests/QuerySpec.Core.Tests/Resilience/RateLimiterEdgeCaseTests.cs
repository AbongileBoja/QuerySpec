using System;
using Microsoft.Extensions.Time.Testing;
using QuerySpec.Core.Resilience;
using Xunit;

namespace QuerySpec.Core.Tests.Resilience;

public class RateLimiterEdgeCaseTests
{
    // ── Argument validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryAcquire_NullOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        var limiter = new RateLimiter();
        Assert.Throws<ArgumentException>(() => limiter.TryAcquire(key!));
    }

    [Fact]
    public void TryAcquire_ZeroTokensRequired_ThrowsArgumentOutOfRange()
    {
        var limiter = new RateLimiter();
        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.TryAcquire("key", 0));
    }

    [Fact]
    public void TryAcquire_NegativeTokensRequired_ThrowsArgumentOutOfRange()
    {
        var limiter = new RateLimiter();
        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.TryAcquire("key", -1));
    }

    [Fact]
    public void TryAcquire_ZeroTokensPerSecond_ThrowsInvalidOperation()
    {
        var limiter = new RateLimiter { TokensPerSecond = 0, BurstSize = 10 };
        Assert.Throws<InvalidOperationException>(() => limiter.TryAcquire("key"));
    }

    [Fact]
    public void TryAcquire_ZeroBurstSize_ThrowsInvalidOperation()
    {
        var limiter = new RateLimiter { TokensPerSecond = 10, BurstSize = 0 };
        Assert.Throws<InvalidOperationException>(() => limiter.TryAcquire("key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRetryAfter_NullOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        var limiter = new RateLimiter();
        Assert.Throws<ArgumentException>(() => limiter.GetRetryAfter(key!));
    }

    [Fact]
    public void GetRetryAfter_ZeroTokensRequired_ThrowsArgumentOutOfRange()
    {
        var limiter = new RateLimiter();
        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.GetRetryAfter("key", 0));
    }

    // ── Drop policy: tokensRequired > BurstSize returns false immediately ────

    [Fact]
    public void TryAcquire_TokensRequiredExceedsBurstSize_ReturnsFalse()
    {
        var limiter = new RateLimiter { TokensPerSecond = 100, BurstSize = 10 };
        var acquired = limiter.TryAcquire("key", 11);
        Assert.False(acquired);
    }

    [Fact]
    public void TryAcquire_TokensRequiredExactlyBurstSize_Succeeds()
    {
        var limiter = new RateLimiter { TokensPerSecond = 100, BurstSize = 10 };
        var acquired = limiter.TryAcquire("key", 10);
        Assert.True(acquired);
    }

    // ── Queue-full (bucket depleted) path ────────────────────────────────────

    [Fact]
    public void TryAcquire_BucketDepleted_ReturnsFalse()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 1, BurstSize = 3 };

        // Drain all 3 tokens.
        Assert.True(limiter.TryAcquire("k"));
        Assert.True(limiter.TryAcquire("k"));
        Assert.True(limiter.TryAcquire("k"));

        // Bucket is empty — should drop.
        Assert.False(limiter.TryAcquire("k"));
    }

    [Fact]
    public void TryAcquire_AfterRefillWindow_AcceptsRequest()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 1, BurstSize = 1 };

        // Drain one token.
        Assert.True(limiter.TryAcquire("k"));
        Assert.False(limiter.TryAcquire("k"));

        // Advance clock by 2 seconds — enough to refill 2 tokens (capped at BurstSize=1).
        fake.Advance(TimeSpan.FromSeconds(2));

        Assert.True(limiter.TryAcquire("k"));
    }

    [Fact]
    public void TryAcquire_MultipleKeys_BucketsAreIndependent()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 10, BurstSize = 1 };

        // Drain key "a".
        Assert.True(limiter.TryAcquire("a"));
        Assert.False(limiter.TryAcquire("a"));

        // Key "b" is a fresh bucket — should succeed.
        Assert.True(limiter.TryAcquire("b"));
    }

    // ── GetRetryAfter paths ───────────────────────────────────────────────────

    [Fact]
    public void GetRetryAfter_BucketNotYetInitialised_ReturnsNull()
    {
        var limiter = new RateLimiter { TokensPerSecond = 10, BurstSize = 10 };
        var retry = limiter.GetRetryAfter("new-key");
        Assert.Null(retry);
    }

    [Fact]
    public void GetRetryAfter_FullBucket_ReturnsNull()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 10, BurstSize = 10 };
        limiter.TryAcquire("k");
        fake.Advance(TimeSpan.FromSeconds(10)); // Refill to burst cap.
        var retry = limiter.GetRetryAfter("k");
        Assert.Null(retry);
    }

    [Fact]
    public void GetRetryAfter_EmptyBucket_ReturnsPositiveTimeSpan()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 2, BurstSize = 2 };

        Assert.True(limiter.TryAcquire("k"));
        Assert.True(limiter.TryAcquire("k"));

        var retry = limiter.GetRetryAfter("k");
        Assert.NotNull(retry);
        Assert.True(retry!.Value > TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryAfter_PartialTokens_ReturnsCorrectWaitTime()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 1, BurstSize = 2 };

        // Drain 2 tokens.
        Assert.True(limiter.TryAcquire("k"));
        Assert.True(limiter.TryAcquire("k"));

        // Need 2 tokens but bucket is empty. Wait for 2/1 = 2 seconds.
        var retry = limiter.GetRetryAfter("k", 2);
        Assert.NotNull(retry);
        Assert.True(retry!.Value.TotalSeconds >= 1.9 && retry.Value.TotalSeconds <= 2.1);
    }

    // ── Partial refill clamped to BurstSize ──────────────────────────────────

    [Fact]
    public void TryAcquire_RefillClampedToBurstSize()
    {
        var fake = new FakeTimeProvider();
        var limiter = new RateLimiter(fake) { TokensPerSecond = 100, BurstSize = 5 };

        // Drain all tokens on first call.
        Assert.True(limiter.TryAcquire("k", 5));

        // Advance 60 seconds — would refill 6000 tokens, but capped at BurstSize=5.
        fake.Advance(TimeSpan.FromSeconds(60));

        // Should succeed because bucket is now full at BurstSize=5.
        Assert.True(limiter.TryAcquire("k", 5));
        // Bucket now at 0 again — next must fail.
        Assert.False(limiter.TryAcquire("k", 1));
    }
}
