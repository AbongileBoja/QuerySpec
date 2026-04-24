using QuerySpec.Core.Resilience;

var backend = new FlakyBackend();

// ---------- 1. Retry with exponential backoff ----------
Console.WriteLine("=== Retry (3 attempts, exponential backoff) ===");
var retry = new RetryPolicy
{
    MaxRetries = 3,
    InitialDelay = TimeSpan.FromMilliseconds(50),
    UseExponentialBackoff = true,
    BackoffMultiplier = 2,
    MaxDelay = TimeSpan.FromSeconds(1)
};

backend.FailNextCalls(2);
var result = await retry.ExecuteAsync(() => backend.QueryAsync("SELECT * FROM orders"));
Console.WriteLine($"  result after retries: {result}\n");

// ---------- 2. Circuit breaker ----------
Console.WriteLine("=== CircuitBreaker (opens after 3 failures) ===");
var breaker = new CircuitBreaker
{
    FailureThreshold = 3,
    OpenTimeout = TimeSpan.FromMilliseconds(500),
    HalfOpenTestInterval = TimeSpan.FromMilliseconds(100)
};

backend.FailForever();
for (var i = 1; i <= 5; i++)
{
    try
    {
        await breaker.ExecuteAsync(() => backend.QueryAsync("SELECT 1"));
    }
    catch (CircuitBreakerOpenException ex)
    {
        Console.WriteLine($"  attempt {i}: breaker OPEN — retry after {ex.RetryAfter.TotalMilliseconds:F0}ms " +
                          $"(failures {ex.FailureCount}/{ex.FailureThreshold})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  attempt {i}: {ex.GetType().Name} — breaker state={breaker.State}");
    }
}
backend.Recover();
Console.WriteLine();

// ---------- 3. Rate limiter (token bucket) ----------
Console.WriteLine("=== RateLimiter (5 tokens/sec, burst 5) ===");
var limiter = new RateLimiter { TokensPerSecond = 5, BurstSize = 5 };
var allowed = 0;
var denied = 0;
for (var i = 0; i < 10; i++)
{
    if (limiter.TryAcquire("tenant-a")) allowed++;
    else denied++;
}
Console.WriteLine($"  10 rapid requests → allowed={allowed}, denied={denied}\n");

// ---------- 4. Bulkhead — cap concurrent work, fail-fast on overflow ----------
Console.WriteLine("=== Bulkhead (max 2 concurrent, fail-fast) ===");
var bulkhead = new BulkheadPolicy(maxConcurrentRequests: 2);
var accepted = 0;
var shed = 0;
var tasks = Enumerable.Range(0, 5).Select(async _ =>
{
    try
    {
        await bulkhead.ExecuteAsync(async () => { await Task.Delay(200); return 0; });
        Interlocked.Increment(ref accepted);
    }
    catch (BulkheadException)
    {
        Interlocked.Increment(ref shed);
    }
}).ToArray();
await Task.WhenAll(tasks);
Console.WriteLine($"  5 concurrent jobs → accepted={accepted}, shed={shed}\n");

// ---------- 5. Composed policy ----------
Console.WriteLine("=== Composed ResiliencePolicy ===");
var policy = new ResiliencePolicy
{
    RateLimiter = new RateLimiter { TokensPerSecond = 100, BurstSize = 10 },
    Bulkhead = new BulkheadPolicy(maxConcurrentRequests: 5),
    CircuitBreaker = new CircuitBreaker { FailureThreshold = 4, OpenTimeout = TimeSpan.FromSeconds(2) },
    RetryPolicy = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromMilliseconds(25), UseExponentialBackoff = true }
};

backend.FailNextCalls(1);
var composed = await policy.ExecuteAsync(
    () => backend.QueryAsync("SELECT * FROM customers WHERE tenant='acme'"),
    key: "tenant-acme");
Console.WriteLine($"  composed call survived one transient failure: {composed}");

public sealed class FlakyBackend
{
    private int _forcedFailures;
    private bool _alwaysFail;
    private int _callCount;

    public void FailNextCalls(int count) { _forcedFailures = count; _alwaysFail = false; }
    public void FailForever() => _alwaysFail = true;
    public void Recover() { _alwaysFail = false; _forcedFailures = 0; }

    public Task<string> QueryAsync(string sql)
    {
        _callCount++;
        if (_alwaysFail)
            throw new InvalidOperationException($"backend down (call #{_callCount})");
        if (_forcedFailures > 0)
        {
            _forcedFailures--;
            throw new TimeoutException($"transient failure (call #{_callCount})");
        }
        return Task.FromResult($"ok:{_callCount}:{sql.Length}b");
    }
}
