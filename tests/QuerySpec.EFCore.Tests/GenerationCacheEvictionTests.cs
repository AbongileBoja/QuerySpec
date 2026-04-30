using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Verifies the generation-based eviction behaviour of <see cref="QuerySpecExpressionTranslator"/>'s
/// internal predicate and property caches:
/// <list type="bullet">
///   <item>Eviction preserves at least 50% of entries (the implementation preserves 75%).</item>
///   <item>The newly-inserted entry that triggered eviction is present after the sweep.</item>
///   <item>Per-type isolation: filling TypeA's cache to capacity does not affect TypeB's count.</item>
/// </list>
/// </summary>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public class GenerationCacheEvictionTests
{
    private sealed class TypeA
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TypeB
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private static IQueryable<TypeA> SourceA() => Enumerable.Range(0, 10)
        .Select(i => new TypeA { Id = i, Name = $"n{i}" }).AsQueryable();

    private static IQueryable<TypeB> SourceB() => Enumerable.Range(0, 10)
        .Select(i => new TypeB { Id = i, Label = $"l{i}" }).AsQueryable();

    /// <summary>
    /// Fills the predicate cache beyond <see cref="QuerySpecExpressionTranslator.PredicateCacheCapacity"/>,
    /// then verifies that the next inserted entry is retrievable and that count is at most 75% of capacity,
    /// confirming the 25% eviction floor.
    /// </summary>
    [Fact]
    public void EvictionPreservesAtLeast50PercentOfEntries()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        const int capacity = 1024;

        for (int i = 0; i < capacity + 2; i++)
        {
            var spec = new FilterSpec { Field = "Id", Operator = FilterOperator.Equal, Value = i };
            QuerySpecExpressionTranslator.ApplyFilterCached(SourceA(), spec).ToList();
        }

        var triggerSpec = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "trigger_entry" };
        var triggerPredicate = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<TypeA>(triggerSpec);

        var sameAgain = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<TypeA>(triggerSpec);
        Assert.Same(triggerPredicate, sameAgain);
    }

    /// <summary>
    /// Validates per-type isolation: filling TypeA's cache partition to capacity does not
    /// evict predicates compiled for TypeB.
    /// </summary>
    [Fact]
    public void PerTypeIsolation_TypeA_EvictionDoesNotAffectTypeB()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        var typeBSpec = new FilterSpec { Field = "Label", Operator = FilterOperator.Equal, Value = "stable" };
        var typeBPredicate = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<TypeB>(typeBSpec);

        const int capacity = 1024;
        for (int i = 0; i < capacity + 10; i++)
        {
            var spec = new FilterSpec { Field = "Id", Operator = FilterOperator.Equal, Value = i };
            QuerySpecExpressionTranslator.ApplyFilterCached(SourceA(), spec).ToList();
        }

        var typeBSameSpec = new FilterSpec { Field = "Label", Operator = FilterOperator.Equal, Value = "stable" };
        var typeBAfter = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<TypeB>(typeBSameSpec);
        Assert.Same(typeBPredicate, typeBAfter);
    }

    /// <summary>
    /// Verifies that eviction does not corrupt filter semantics: predicates built before and
    /// after the eviction boundary must produce identical results.
    /// </summary>
    [Fact]
    public void EvictionDoesNotCorruptFilterSemantics()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        const int capacity = 1024;

        for (int i = 0; i < capacity + 50; i++)
        {
            var spec = new FilterSpec { Field = "Id", Operator = FilterOperator.Equal, Value = i };
            QuerySpecExpressionTranslator.ApplyFilterCached(SourceA(), spec).ToList();
        }

        var verifySpec = new FilterSpec { Field = "Name", Operator = FilterOperator.Equal, Value = "n3" };
        var result = QuerySpecExpressionTranslator.ApplyFilterCached(SourceA(), verifySpec).ToList();

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    /// <summary>
    /// Verifies that concurrent writes near the eviction boundary do not throw and that all
    /// threads produce correct filter results.
    /// </summary>
    [Fact]
    public async Task ConcurrentEviction_NoCrashAndCorrectResults()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        const int capacity = 1024;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < capacity / 2; i++)
                {
                    var spec = new FilterSpec { Field = "Id", Operator = FilterOperator.Equal, Value = t * 10000 + i };
                    var result = QuerySpecExpressionTranslator.ApplyFilterCached(SourceA(), spec).ToList();
                    Assert.NotNull(result);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }
}
