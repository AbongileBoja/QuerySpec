using System.Linq;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Unit tests for <see cref="QuerySpecExpressionTranslator.ApplyFilterCached{T}"/>. Verifies
/// that the cache produces the same results as <see cref="QuerySpecExpressionTranslator.ApplyFilter{T}"/>,
/// that structurally-equal filters share the same compiled expression instance, and that
/// validation failures still propagate on cache miss.
/// </summary>
public class ApplyFilterCachedTests
{
    private sealed class Widget
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private static IQueryable<Widget> Source() =>
        Enumerable.Range(0, 50)
            .Select(i => new Widget { Id = i, Category = i % 2 == 0 ? "A" : "B", Price = i * 2m })
            .AsQueryable();

    [Fact]
    public void Cached_Matches_Uncached_Results()
    {
        var filter = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
        };

        QuerySpecExpressionTranslator.ClearPredicateCache();
        var expected = QuerySpecExpressionTranslator.ApplyFilter(Source(), filter).ToList();
        var actual = QuerySpecExpressionTranslator.ApplyFilterCached(Source(), filter).ToList();

        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.Select(w => w.Id), actual.Select(w => w.Id));
    }

    [Fact]
    public void Cached_Reuses_Compiled_Expression_For_Equivalent_Filters()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var a = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
        };
        var b = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
        };

        var exprA = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(a);
        var exprB = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(b);

        Assert.Same(exprA, exprB);
    }

    [Fact]
    public void Cached_BuildsNew_Expression_When_Filter_Differs()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var a = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
        };
        var b = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "B",
        };

        var exprA = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(a);
        var exprB = QuerySpecExpressionTranslator.GetOrBuildCachedPredicate<Widget>(b);

        Assert.NotSame(exprA, exprB);
    }

    [Fact]
    public void Cached_NullFilter_ReturnsOriginalQuery()
    {
        var source = Source();
        var result = QuerySpecExpressionTranslator.ApplyFilterCached<Widget>(source, null);
        Assert.Same(source, result);
    }

    [Fact]
    public void Cached_InvalidFilter_Throws_OnFirstBuild()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();
        var filter = new FilterSpec
        {
            Field = "bad name!", // fails FieldNamePattern
            Operator = FilterOperator.Equal,
            Value = "x",
        };

        Assert.Throws<System.ArgumentException>(() =>
            QuerySpecExpressionTranslator.ApplyFilterCached(Source(), filter).ToList());
    }
}
