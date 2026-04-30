using System;
using System.Collections.Concurrent;

namespace QuerySpec.Core.Resilience;

/// <summary>
/// Thread-safe token bucket rate limiter. Each key gets its own bucket with an independent
/// lock so keys do not contend with each other; the per-bucket lock guarantees that token
/// refill and consumption are atomic.
/// </summary>
/// <remarks>
/// Uses <see cref="TimeProvider.GetTimestamp"/> (monotonic) rather than wall-clock <c>DateTime.UtcNow</c>
/// so that wall-clock adjustments (NTP, DST) cannot cause negative elapsed times or token surges.
/// Lock-based rather than lock-free: benchmarks showed that a CAS-on-reference design allocated
/// a state object per successful update and regressed under same-key contention (CAS retries
/// burn CPU where a Monitor parks). For a rate limiter, same-key serialization is the expected
/// shape — callers are already being throttled on that key — so the managed lock is the right
/// primitive.
/// </remarks>
public sealed class RateLimiter
{
    private sealed class Bucket
    {
#if NET9_0_OR_GREATER
        public readonly System.Threading.Lock Sync = new();
#else
        public readonly object Sync = new();
#endif
        public double Tokens;
        public long LastRefillTicks;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="RateLimiter"/>. An optional <paramref name="timeProvider"/>
    /// allows tests to drive the refill clock deterministically; production code omits it and
    /// receives <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// Clock used for refill-window calculations. Defaults to <see cref="TimeProvider.System"/>
    /// when <c>null</c>. Inject <c>Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider</c>
    /// in tests to avoid wall-clock races.
    /// </param>
    public RateLimiter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Number of tokens to add per second. Must be positive.</summary>
    public int TokensPerSecond { get; set; } = 100;

    /// <summary>Maximum number of tokens that can be accumulated. Must be positive.</summary>
    public int BurstSize { get; set; } = 150;

    /// <summary>
    /// Tries to acquire tokens for a key. Returns <c>true</c> if the requested tokens were
    /// consumed; <c>false</c> otherwise. Thread-safe under concurrent access to the same key.
    /// </summary>
    /// <param name="key">Bucket identifier. Must not be null, empty, or whitespace.</param>
    /// <param name="tokensRequired">Number of tokens the call is requesting. Must be positive.</param>
    /// <returns><c>true</c> when the bucket had enough tokens and they were consumed; <c>false</c> otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tokensRequired"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="TokensPerSecond"/> or <see cref="BurstSize"/> is non-positive.</exception>
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
            var now = _timeProvider.GetTimestamp();
            if (bucket.LastRefillTicks == 0)
            {
                bucket.LastRefillTicks = now;
                bucket.Tokens = BurstSize;
            }
            else
            {
                var elapsed = _timeProvider.GetElapsedTime(bucket.LastRefillTicks, now);
                var elapsedSeconds = elapsed.TotalSeconds;
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
    /// <param name="key">Bucket identifier. Must not be null, empty, or whitespace.</param>
    /// <param name="tokensRequired">Number of tokens the inspection is sized for. Must be positive.</param>
    /// <returns>The time-to-availability for the requested tokens, or <c>null</c> when the bucket already has enough.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tokensRequired"/> is not positive.</exception>
    public TimeSpan? GetRetryAfter(string key, int tokensRequired = 1)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be null or empty.", nameof(key));
        if (tokensRequired <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokensRequired), "tokensRequired must be positive.");

        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());

        lock (bucket.Sync)
        {
            var now = _timeProvider.GetTimestamp();
            double tokens;
            if (bucket.LastRefillTicks == 0)
            {
                tokens = BurstSize;
            }
            else
            {
                var elapsed = _timeProvider.GetElapsedTime(bucket.LastRefillTicks, now);
                tokens = Math.Min(BurstSize, bucket.Tokens + Math.Max(0, elapsed.TotalSeconds) * TokensPerSecond);
            }

            if (tokens >= tokensRequired)
                return null;

            var deficit = tokensRequired - tokens;
            var seconds = deficit / TokensPerSecond;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
