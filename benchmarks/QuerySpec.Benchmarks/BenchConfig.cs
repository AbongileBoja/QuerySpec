using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Shared benchmark configuration. The default BenchmarkDotNet job infers iteration counts
/// from measured variance, which on a loaded dev host can produce high error bars (the
/// previous run showed StdDev > 50% of Mean on several benchmarks). This config pins the
/// pilot, warmup, and measurement phases to conservative values so results are reproducible
/// and suitable for enterprise publication.
/// </summary>
/// <remarks>
/// When the <c>QUERYSPEC_BENCH_SMOKE</c> environment variable is set to <c>1</c>, the config
/// switches to a single-iteration <see cref="Job.Dry"/> job and skips the markdown exporter.
/// This is used by the release pipeline as a crash / 100x-regression gate that completes in
/// seconds rather than minutes.
/// </remarks>
public sealed class BenchConfig : ManualConfig
{
    private static bool IsSmokeMode =>
        string.Equals(Environment.GetEnvironmentVariable("QUERYSPEC_BENCH_SMOKE"), "1", StringComparison.Ordinal);

    /// <summary>Builds the shared config.</summary>
    public BenchConfig()
    {
        AddJob(IsSmokeMode
            ? Job.Dry
            : Job.Default
                .WithLaunchCount(2)          // two separate processes — catches JIT/noise bias
                .WithWarmupCount(5)
                .WithIterationCount(15));

        AddDiagnoser(MemoryDiagnoser.Default);

        if (!IsSmokeMode)
        {
            AddExporter(MarkdownExporter.GitHub);
        }
    }
}
