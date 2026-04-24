# QuerySpec Benchmarks

BenchmarkDotNet suite covering the performance-critical paths of QuerySpec.

## Running

```bash
# Full suite (takes ~20-40 minutes depending on CPU)
dotnet run -c Release --project benchmarks/QuerySpec.Benchmarks -- --filter "*"

# Single class
dotnet run -c Release --project benchmarks/QuerySpec.Benchmarks -- --filter "*TranslatorBenchmarks*"

# Single method
dotnet run -c Release --project benchmarks/QuerySpec.Benchmarks -- --filter "*SimpleEqual*"

# Quick smoke (dramatically fewer iterations; useful for CI smoke tests, not for perf reports)
dotnet run -c Release --project benchmarks/QuerySpec.Benchmarks -- --filter "*" --job short
```

Results are written to `BenchmarkDotNet.Artifacts/results/` as CSV, HTML, and GitHub-flavored Markdown.

## Suites

| Class                    | What it measures                                                                               |
|--------------------------|------------------------------------------------------------------------------------------------|
| `TranslatorBenchmarks`   | Predicate construction + execution for Equal, Compound AND, nested OR-of-ANDs, and IN of 100.  |
| `RateLimiterBenchmarks`  | Token-bucket throughput single-threaded and under contention (1/4/16 threads, same/distinct keys). |
| `CacheBenchmarks`        | Memory, distributed (in-memory stub), and multi-level L1-hit round-trips.                      |

## Notes

- Pinned to `net10.0` only. BDN's auto-generated child project must agree with the benchmarks
  project's TFM, which is why `benchmarks/Directory.Build.props` overrides the root multi-target
  to a single framework. Change the TFM in `benchmarks/Directory.Build.props` to measure on
  net8.0 / net9.0 instead.
- For stable numbers, close all other applications and disable CPU frequency scaling before
  running. BDN sets the High Performance power plan automatically on Windows.
