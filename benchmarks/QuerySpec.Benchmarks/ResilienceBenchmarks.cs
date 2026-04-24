using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Hot-path benchmarks for the resilience primitives that weren't previously covered:
/// CircuitBreaker (closed-state success), RetryPolicy (no-retry path), and Bulkhead
/// admission. All three sit on request hot paths, so their per-call overhead matters
/// for enterprise throughput budgeting.
/// </summary>
[Config(typeof(BenchConfig))]
public class ResilienceBenchmarks
{
    private CircuitBreaker _breaker = null!;
    private RetryPolicy _retry = null!;
    private BulkheadPolicy _bulkhead = null!;
    private static readonly Task<int> CompletedInt = Task.FromResult(1);

    /// <summary>Seeds fresh resilience primitives before each run.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _breaker = new CircuitBreaker { FailureThreshold = 5, OpenTimeout = TimeSpan.FromSeconds(1) };
        _retry = new RetryPolicy { MaxRetries = 3, UseExponentialBackoff = false };
        _bulkhead = new BulkheadPolicy(maxConcurrentRequests: 32);
    }

    /// <summary>Closed-circuit success path: measures lock + state read overhead.</summary>
    [Benchmark]
    public async Task<int> CircuitBreaker_Success()
        => await _breaker.ExecuteAsync(() => CompletedInt);

    /// <summary>Retry policy with a first-try success — measures the common path with no retries.</summary>
    [Benchmark]
    public async Task<int> RetryPolicy_FirstTrySuccess()
        => await _retry.ExecuteAsync(() => CompletedInt);

    /// <summary>Bulkhead admission + release under no contention.</summary>
    [Benchmark]
    public async Task<int> Bulkhead_Admit_Release()
        => await _bulkhead.ExecuteAsync(() => CompletedInt);
}
