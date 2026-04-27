using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Shared benchmark configuration.
/// </summary>
/// <remarks>
/// Three modes driven by environment variables:
/// <list type="bullet">
/// <item><c>QUERYSPEC_BENCH_SMOKE=1</c> — single <see cref="Job.Dry"/> iteration, crash-gate only.</item>
/// <item><c>QUERYSPEC_BENCH_GATE=1</c> — <see cref="Job.ShortRun"/> (3 launches × 5 warmups × 10 iterations),
///   JSON exported for regression comparison against committed baseline.</item>
/// <item>Neither — full <see cref="Job.Default"/> with pinned iteration counts, Markdown + JSON.</item>
/// </list>
/// </remarks>
public sealed class BenchConfig : ManualConfig
{
    private static bool IsSmokeMode =>
        string.Equals(Environment.GetEnvironmentVariable("QUERYSPEC_BENCH_SMOKE"), "1", StringComparison.Ordinal);

    private static bool IsGateMode =>
        string.Equals(Environment.GetEnvironmentVariable("QUERYSPEC_BENCH_GATE"), "1", StringComparison.Ordinal);

    /// <summary>Builds the shared config.</summary>
    public BenchConfig()
    {
        if (IsSmokeMode)
        {
            AddJob(Job.Dry);
        }
        else if (IsGateMode)
        {
            AddJob(Job.ShortRun
                .WithLaunchCount(3)
                .WithWarmupCount(5)
                .WithIterationCount(10));
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(JsonExporter.Full);
        }
        else
        {
            AddJob(Job.Default
                .WithLaunchCount(2)
                .WithWarmupCount(5)
                .WithIterationCount(15));
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(JsonExporter.Full);
        }
    }
}
