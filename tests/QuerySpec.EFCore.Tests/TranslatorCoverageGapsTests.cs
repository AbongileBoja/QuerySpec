using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Focused coverage for translator branches the test-engineer audit (#69) flagged at 0% from
/// the EFCore test project: <c>NormalizeValue</c> JSON coercion paths, <c>ExtractArrayValues</c>
/// non-JSON enumerables, nullable comparison/temporal paths, and the predicate cache eviction branch.
/// </summary>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public class TranslatorCoverageGapsTests
{
    private sealed class Entity
    {
        public int Id { get; set; }
        public int Age { get; set; }
        public decimal? Salary { get; set; }
        public long? AccountBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsActive { get; set; }
    }

    private static IQueryable<Entity> Source() => new[]
    {
        new Entity { Id = 1, Age = 25, Salary = 1000m, AccountBalance = 50000L,
            CreatedAt = new DateTime(2024, 1, 1), DeletedAt = null, IsActive = true },
        new Entity { Id = 2, Age = 30, Salary = null, AccountBalance = null,
            CreatedAt = new DateTime(2024, 6, 1), DeletedAt = new DateTime(2025, 1, 1), IsActive = false },
        new Entity { Id = 3, Age = 35, Salary = 3000m, AccountBalance = 25000L,
            CreatedAt = new DateTime(2024, 9, 1), DeletedAt = null, IsActive = true },
    }.AsQueryable();

    private static List<Entity> Run(FilterSpec filter) =>
        QuerySpecExpressionTranslator.ApplyFilter(Source(), filter).ToList();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    // ── NormalizeValue: JsonElement coercion ────────────────────────────────

    [Fact]
    public void Normalize_JsonNumber_CoercesToInt()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.Equal,
            Value = Json("30"),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void Normalize_JsonNumber_CoercesToDecimal()
    {
        var filter = new FilterSpec
        {
            Field = "Salary",
            Operator = FilterOperator.Equal,
            Value = Json("3000"),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void Normalize_JsonNumber_CoercesToLong()
    {
        var filter = new FilterSpec
        {
            Field = "AccountBalance",
            Operator = FilterOperator.Equal,
            Value = Json("50000"),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void Normalize_JsonString_ParsesDateTime()
    {
        var filter = new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.Equal,
            Value = Json("\"2024-06-01\""),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void Normalize_JsonBool_TrueFalse()
    {
        var trueFilter = new FilterSpec
        {
            Field = "IsActive",
            Operator = FilterOperator.Equal,
            Value = Json("true"),
        };
        Assert.Equal(2, Run(trueFilter).Count);

        var falseFilter = new FilterSpec
        {
            Field = "IsActive",
            Operator = FilterOperator.Equal,
            Value = Json("false"),
        };
        var falseResult = Run(falseFilter);
        Assert.Single(falseResult);
        Assert.Equal(2, falseResult[0].Id);
    }

    [Fact]
    public void Normalize_StringNumber_CoercesViaConvert()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.Equal,
            Value = "35",
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void Normalize_NumericOverflow_ThrowsArgumentException()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.Equal,
            Value = (long)int.MaxValue + 1,
        };

        var ex = Assert.Throws<ArgumentException>(() => Run(filter));
        Assert.Contains("Unable to convert value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_InvalidStringCoercion_ThrowsArgumentException()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.Equal,
            Value = "not-a-number",
        };

        var ex = Assert.Throws<ArgumentException>(() => Run(filter));
        Assert.Contains("Unable to convert value", ex.Message, StringComparison.Ordinal);
    }

    // ── ExtractArrayValues: non-JsonArray enumerable + scalar fallthrough ───

    [Fact]
    public void ExtractArrayValues_FromIEnumerable_NotJsonArray_BuildsIn()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.In,
            Value = new List<int> { 25, 35 },
        };
        var result = Run(filter).Select(e => e.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void ExtractArrayValues_FromJsonArray_BuildsIn()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.In,
            Value = Json("[25, 35]"),
        };
        var result = Run(filter).Select(e => e.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void ExtractArrayValues_FromScalar_WrapsToSingleItem()
    {
        var filter = new FilterSpec
        {
            Field = "Age",
            Operator = FilterOperator.In,
            Value = 30,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── nullable comparison paths ────────────────────────────────────────────

    [Fact]
    public void NullableDecimal_Equal_MatchesValue()
    {
        var filter = new FilterSpec
        {
            Field = "Salary",
            Operator = FilterOperator.Equal,
            Value = 3000m,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void NullableDecimal_GreaterThan_ExcludesNullAndLowerValues()
    {
        var filter = new FilterSpec
        {
            Field = "Salary",
            Operator = FilterOperator.GreaterThan,
            Value = 2000m,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void NullableLong_LessThanOrEqual_ExcludesNull()
    {
        var filter = new FilterSpec
        {
            Field = "AccountBalance",
            Operator = FilterOperator.LessThanOrEqual,
            Value = 50000L,
        };
        var result = Run(filter).Select(e => e.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 3 }, result);
    }

    // ── nullable temporal paths ─────────────────────────────────────────────

    [Fact]
    public void NullableDateTime_DateAfter_ExcludesNull()
    {
        var filter = new FilterSpec
        {
            Field = "DeletedAt",
            Operator = FilterOperator.DateAfter,
            Value = new DateTime(2024, 12, 1),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void NullableDateTime_DateInRange_ExcludesNull()
    {
        // DateInRange consults TemporalStart/TemporalEnd, not Value/ValueTo. When those are
        // unset the operator silently falls through to Constant(true) (audit-flagged at #78);
        // this test exercises the supported shape.
        var filter = new FilterSpec
        {
            Field = "DeletedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2024, 12, 1),
            TemporalEnd = new DateTime(2025, 12, 31),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void NullableDecimal_Between_ExcludesNullAndOutOfRange()
    {
        var filter = new FilterSpec
        {
            Field = "Salary",
            Operator = FilterOperator.Between,
            Value = 500m,
            ValueTo = 2000m,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    // ── predicate cache generation eviction ────────────────────────────────

    /// <summary>
    /// PredicateCacheCapacity (1024) distinct filter shapes must trigger the generation-based
    /// eviction sweep (drops bottom 25% by epoch). After eviction, subsequent filter builds
    /// must still produce correct predicates.
    /// </summary>
    [Fact]
    public void PredicateCache_BulkEviction_DoesNotCorruptResults()
    {
        QuerySpecExpressionTranslator.ClearPredicateCache();

        // Generate 1100 distinct filter shapes (>capacity of 1024) so the eviction sweep fires.
        for (var i = 0; i < 1100; i++)
        {
            var filter = new FilterSpec
            {
                Field = "Id",
                Operator = FilterOperator.Equal,
                Value = i,
            };
            QuerySpecExpressionTranslator.ApplyFilterCached(Source(), filter).ToList();
        }

        // After eviction churn, a fresh filter must still produce the right rows.
        var verification = QuerySpecExpressionTranslator
            .ApplyFilterCached(Source(), new FilterSpec
            {
                Field = "Id",
                Operator = FilterOperator.Equal,
                Value = 2,
            })
            .ToList();
        Assert.Single(verification);
        Assert.Equal(2, verification[0].Id);
    }
}
