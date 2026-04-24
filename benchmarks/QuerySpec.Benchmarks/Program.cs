using BenchmarkDotNet.Running;

namespace QuerySpec.Benchmarks;

/// <summary>Entry point for BenchmarkDotNet. Run with: dotnet run -c Release -- --filter *</summary>
public static class Program
{
    /// <summary>Dispatches to BenchmarkSwitcher so callers can filter with <c>--filter</c>.</summary>
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
