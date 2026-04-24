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
public sealed class BenchConfig : ManualConfig
{
    /// <summary>Builds the shared config.</summary>
    public BenchConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(2)          // two separate processes — catches JIT/noise bias
            .WithWarmupCount(5)
            .WithIterationCount(15));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(MarkdownExporter.GitHub);
    }
}
