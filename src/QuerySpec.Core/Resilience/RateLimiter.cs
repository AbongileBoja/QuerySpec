using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Thread-safe token bucket rate limiter. Each key gets its own bucket with an independent
/// lock so keys do not contend with each other; the per-bucket lock guarantees that token
/// refill and consumption are atomic.
/// </summary>
/// <remarks>
/// Uses <see cref="Stopwatch"/> timestamps (monotonic) rather than <see cref="DateTime.UtcNow"/>
/// so that wall-clock adjustments (NTP, DST) cannot cause negative elapsed times or token surges.
/// Lock-based rather than lock-free: benchmarks showed that a CAS-on-reference design allocated
/// a state object per successful update and regressed under same-key contention (CAS retries
/// burn CPU where a Monitor parks). For a rate limiter, same-key serialization is the expected
/// shape — callers are already being throttled on that key — so the managed lock is the right
/// primitive.
/// </remarks>
public class RateLimiter
{
    private sealed class Bucket
    {
        public readonly object Sync = new();
        public double Tokens;
        public long LastRefillTicks;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets =
        new(StringComparer.Ordinal);

    /// <summary>Number of tokens to add per second. Must be positive.</summary>
    public int TokensPerSecond { get; set; } = 100;

    /// <summary>Maximum number of tokens that can be accumulated. Must be positive.</summary>
    public int BurstSize { get; set; } = 150;

    /// <summary>
    /// Tries to acquire tokens for a key. Returns <c>true</c> if the requested tokens were
    /// consumed; <c>false</c> otherwise. Thread-safe under concurrent access to the same key.
    /// </summary>
    public bool TryAcquire(string key, int tokensRequired = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be null or empty.", nameof(key));
        if (tokensRequired <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensRequired), "tokensRequired must be positive.");
        if (TokensPerSecond <= 0)
            throw new InvalidOperationException("TokensPerSecond must be positive.");
        if (BurstSize <= 0)
            throw new InvalidOperationException("BurstSize must be positive.");
        if (tokensRequired > BurstSize)
            return false;

        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());

        lock (bucket.Sync)
        {
            var now = Stopwatch.GetTimestamp();
            if (bucket.LastRefillTicks == 0)
            {
                bucket.LastRefillTicks = now;
                bucket.Tokens = BurstSize;
            }
            else
            {
                var elapsedSeconds = (now - bucket.LastRefillTicks) / (double)Stopwatch.Frequency;
                if (elapsedSeconds > 0)
                {
                    bucket.Tokens = Math.Min(BurstSize, bucket.Tokens + elapsedSeconds * TokensPerSecond);
                    bucket.LastRefillTicks = now;
                }
            }

            if (bucket.Tokens >= tokensRequired)
            {
                bucket.Tokens -= tokensRequired;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Returns the time remaining until enough tokens are available to satisfy the request,
    /// or <c>null</c> if the request can be satisfied immediately. Does not consume tokens.
    /// </summary>
    public TimeSpan? GetRetryAfter(string key, int tokensRequired = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be null or empty.", nameof(key));
        if (tokensRequired <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensRequired), "tokensRequired must be positive.");

        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());

        lock (bucket.Sync)
        {
            var now = Stopwatch.GetTimestamp();
            double tokens;
            if (bucket.LastRefillTicks == 0)
            {
                tokens = BurstSize;
            }
            else
            {
                var elapsedSeconds = (now - bucket.LastRefillTicks) / (double)Stopwatch.Frequency;
                tokens = Math.Min(BurstSize, bucket.Tokens + Math.Max(0, elapsedSeconds) * TokensPerSecond);
            }

            if (tokens >= tokensRequired)
                return null;

            var deficit = tokensRequired - tokens;
            var seconds = deficit / TokensPerSecond;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
