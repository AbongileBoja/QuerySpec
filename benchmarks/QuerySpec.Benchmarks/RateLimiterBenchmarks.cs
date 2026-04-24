using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Resilience;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Measures the RateLimiter under single-threaded and contended workloads. The contended
/// workload exercises the per-bucket lock that replaces the previous global lock.
/// </summary>
[Config(typeof(BenchConfig))]
public class RateLimiterBenchmarks
{
    private RateLimiter _limiter = null!;

    /// <summary>Parallelism used for contended benchmarks.</summary>
    [Params(1, 4, 16)]
    public int Threads { get; set; }

    /// <summary>Allocates a high-capacity limiter so TryAcquire never drains the bucket.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _limiter = new RateLimiter { TokensPerSecond = 1_000_000, BurstSize = 1_000_000 };
    }

    /// <summary>Single-key throughput — measures per-call cost of TryAcquire.</summary>
    [Benchmark]
    public int SingleKey_TryAcquire()
    {
        var ok = 0;
        for (var i = 0; i < 10_000; i++)
            if (_limiter.TryAcquire("k")) ok++;
        return ok;
    }

    /// <summary>
    /// Contended path: N threads each hammer the same key. With the old global lock this
    /// was serialized across all keys; with per-bucket locks this is the expected case.
    /// </summary>
    [Benchmark]
    public void Contended_SameKey()
    {
        var tasks = new Task[Threads];
        for (var t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < 10_000; i++)
                    _limiter.TryAcquire("shared");
            });
        }
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Distinct-key workload: N threads, each with their own key. Should scale linearly
    /// because per-bucket locks do not contend across keys.
    /// </summary>
    [Benchmark]
    public void Contended_DistinctKeys()
    {
        var tasks = new Task[Threads];
        for (var t = 0; t < Threads; t++)
        {
            var id = t;
            tasks[t] = Task.Run(() =>
            {
                var key = "k" + id;
                for (var i = 0; i < 10_000; i++)
                    _limiter.TryAcquire(key);
            });
        }
        Task.WaitAll(tasks);
    }
}
