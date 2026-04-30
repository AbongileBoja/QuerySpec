using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Measures cache eviction behaviour at the capacity boundary under concurrent load.
/// Compares the eviction-boundary latency profile between a cold-fill scenario (which
/// would previously trigger <c>ConcurrentDictionary.Clear()</c>) and the post-fix
/// generation-based sweep that preserves 75% of entries on each pass.
/// </summary>
[Config(typeof(BenchConfig))]
[RequiresUnreferencedCode("Benchmark exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Benchmark exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public class PredicateCacheEvictionBenchmarks
{
    private const int ThreadCount = 8;
    private const int FiltersPerThread = 200;

    [GlobalSetup]
    public void Setup()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
    }

    /// <summary>
    /// Builds 1600 distinct predicates across 8 concurrent threads, crossing the 1024-entry
    /// eviction boundary. Each thread builds a non-overlapping set of field names so every
    /// predicate is a genuine cache miss on the first build. Measures the wall-clock cost of
    /// the generation-based eviction sweep vs the previous bulk-clear approach.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentBuild_AboveCapacity()
    {
        var tasks = Enumerable.Range(0, ThreadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < FiltersPerThread; i++)
            {
                var spec = new FilterSpec
                {
                    Field = "Name",
                    Operator = FilterOperator.Contains,
                    Value = $"v_{t}_{i}"
                };
                QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<EvictionEntity>(spec);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Same workload split across two distinct entity types to verify per-type isolation:
    /// <see cref="EvictionEntity"/> and <see cref="EvictionEntity2"/> each have their own
    /// partition so one type's eviction does not affect the other.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentBuild_TwoEntityTypes()
    {
        var tasks = Enumerable.Range(0, ThreadCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < FiltersPerThread / 2; i++)
            {
                var spec = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = $"e1_{t}_{i}" };
                QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<EvictionEntity>(spec);

                var spec2 = new FilterSpec { Field = "Label", Operator = FilterOperator.Equal, Value = $"e2_{t}_{i}" };
                QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<EvictionEntity2>(spec2);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>Entity used for eviction benchmarks.</summary>
    public sealed class EvictionEntity
    {
        /// <summary>Identifier.</summary>
        public int Id { get; set; }
        /// <summary>Name.</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Second entity type for per-type isolation benchmark.</summary>
    public sealed class EvictionEntity2
    {
        /// <summary>Identifier.</summary>
        public int Id { get; set; }
        /// <summary>Label.</summary>
        public string Label { get; set; } = string.Empty;
    }
}
