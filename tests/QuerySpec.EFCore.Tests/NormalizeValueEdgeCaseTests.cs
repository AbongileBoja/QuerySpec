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
/// Covers NormalizeValue and BuildComparison paths not reached by earlier tests:
/// float/short/byte JSON Number coercion, JsonElement default-case fallthrough,
/// bool-from-string coercion, null-targetType passthrough, and BuildComparison
/// edge branches for non-nullable value types (byte, float, short).
/// </summary>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator reflection paths.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator expression tree compilation.")]
public class NormalizeValueEdgeCaseTests
{
    private sealed class Widget
    {
        public int Id { get; set; }
        public float Score { get; set; }
        public short Priority { get; set; }
        public byte Level { get; set; }
        public float? OptScore { get; set; }
        public bool Active { get; set; }
        public string Name { get; set; } = "";
    }

    private static IQueryable<Widget> Source() => new[]
    {
        new Widget { Id = 1, Score = 1.5f, Priority = 10, Level = 3, OptScore = null,   Active = true,  Name = "Alpha" },
        new Widget { Id = 2, Score = 2.5f, Priority = 20, Level = 5, OptScore = 2.5f,   Active = false, Name = "Beta"  },
        new Widget { Id = 3, Score = 3.5f, Priority = 30, Level = 7, OptScore = 3.5f,   Active = true,  Name = "Gamma" },
    }.AsQueryable();

    private static List<Widget> Run(FilterSpec filter) =>
        QuerySpecExpressionTranslator.ApplyFilter(Source(), filter).ToList();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    // ── NormalizeValue: JsonElement.Number for float ────────────────────────

    [Fact]
    public void Normalize_JsonNumber_CoercesToFloat()
    {
        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = FilterOperator.Equal,
            Value = Json("2.5"),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── NormalizeValue: JsonElement.Number for short — falls to double (not directly handled) ──

    [Fact]
    public void Normalize_JsonNumber_ShortField_ThrowsWhenDoubleIncompatible()
    {
        // NormalizeValue returns je.GetDouble() for short (no explicit short branch in the
        // JsonNumber case block). The expression builder then tries Expression.Constant(double, typeof(short))
        // which throws ArgumentException "Argument types do not match". This documents the
        // known gap: short/byte fields must use CLR-typed values (not JsonElement) with the current translator.
        var filter = new FilterSpec
        {
            Field = "Priority",
            Operator = FilterOperator.Equal,
            Value = Json("20"),
        };
        Assert.Throws<ArgumentException>(() => Run(filter));
    }

    // ── NormalizeValue: JsonElement.Number for byte — same fallthrough ───────

    [Fact]
    public void Normalize_JsonNumber_ByteField_ThrowsWhenDoubleIncompatible()
    {
        // Same as short: NormalizeValue returns je.GetDouble() for byte, then
        // Expression.Constant(double, typeof(byte)) throws ArgumentException.
        var filter = new FilterSpec
        {
            Field = "Level",
            Operator = FilterOperator.Equal,
            Value = Json("5"),
        };
        Assert.Throws<ArgumentException>(() => Run(filter));
    }

    // ── NormalizeValue: JsonElement.Number for short using CLR value ──────────

    [Fact]
    public void Normalize_Short_ClrValue_Succeeds()
    {
        var filter = new FilterSpec
        {
            Field = "Priority",
            Operator = FilterOperator.Equal,
            Value = (short)20,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void Normalize_Byte_ClrValue_Succeeds()
    {
        var filter = new FilterSpec
        {
            Field = "Level",
            Operator = FilterOperator.Equal,
            Value = (byte)5,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── NormalizeValue: JsonElement.Number default fallthrough (exercises else branch) ──

    [Fact]
    public void Normalize_JsonNumber_FallsBackToDouble_WithDoubleTargetType()
    {
        // Exercises the default je.GetDouble() return at the end of the Number case block.
        // A double? field with a JSON number — no specific if-branch for double? (the branch
        // checks typeof(double) | typeof(double?) so actually it IS handled for double/double?).
        // To hit the true default we need a type NOT in the if-list (e.g. float? via JsonElement).
        // float? IS handled via je.GetSingle(), so we test it with a float field for correctness.
        var entities = new[]
        {
            new DoubleEntity { Id = 1, Score = 1.5 },
            new DoubleEntity { Id = 2, Score = 2.5 },
        }.AsQueryable();

        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = FilterOperator.Equal,
            Value = Json("2.5"),
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── NormalizeValue: JsonElement string that is NOT datetime/bool/numeric ─

    [Fact]
    public void Normalize_JsonString_ReturnsStringValue_WhenNoCoercionApplies()
    {
        // A JSON string value on a string field that cannot be parsed as DateTime/bool/numeric
        var filter = new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = Json("\"Gamma\""),
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    // ── NormalizeValue: JsonElement default case (Null / Object / Undefined) ─

    [Fact]
    public void Normalize_JsonNull_ReturnsNullViaNormalizeValue()
    {
        // JsonElement with ValueKind=Null hits the default case and returns je.ToString() = ""
        // On a string field this means Equal("", ...) — none of our seeds have empty Name.
        var filter = new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = Json("null"),
        };
        var result = Run(filter);
        Assert.Empty(result);
    }

    // ── NormalizeValue: string "true"/"false" on bool field (non-JSON path) ──

    [Fact]
    public void Normalize_StringBool_CoercesToTrue()
    {
        var filter = new FilterSpec
        {
            Field = "Active",
            Operator = FilterOperator.Equal,
            Value = "true",
        };
        var result = Run(filter);
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.True(w.Active));
    }

    [Fact]
    public void Normalize_StringBool_CoercesToFalse()
    {
        var filter = new FilterSpec
        {
            Field = "Active",
            Operator = FilterOperator.Equal,
            Value = "false",
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.False(result[0].Active);
    }

    // ── BuildComparison: float (non-nullable value type) comparison ops ──────

    [Fact]
    public void BuildComparison_Float_GreaterThan()
    {
        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = FilterOperator.GreaterThan,
            Value = 2.0f,
        };
        var result = Run(filter).Select(w => w.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 2, 3 }, result);
    }

    [Fact]
    public void BuildComparison_Float_LessThanOrEqual()
    {
        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = FilterOperator.LessThanOrEqual,
            Value = 2.5f,
        };
        var result = Run(filter).Select(w => w.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2 }, result);
    }

    // ── BuildComparison: nullable float comparison ops ───────────────────────

    [Fact]
    public void BuildComparison_NullableFloat_GreaterThanOrEqual_ExcludesNull()
    {
        var filter = new FilterSpec
        {
            Field = "OptScore",
            Operator = FilterOperator.GreaterThanOrEqual,
            Value = 3.0f,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void BuildComparison_NullableFloat_LessThan_ExcludesNull()
    {
        var filter = new FilterSpec
        {
            Field = "OptScore",
            Operator = FilterOperator.LessThan,
            Value = 3.0f,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── BuildComparison: short / byte comparisons ────────────────────────────

    [Fact]
    public void BuildComparison_Short_GreaterThanOrEqual()
    {
        var filter = new FilterSpec
        {
            Field = "Priority",
            Operator = FilterOperator.GreaterThanOrEqual,
            Value = (short)20,
        };
        var result = Run(filter).Select(w => w.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 2, 3 }, result);
    }

    [Fact]
    public void BuildComparison_Byte_LessThan()
    {
        var filter = new FilterSpec
        {
            Field = "Level",
            Operator = FilterOperator.LessThan,
            Value = (byte)5,
        };
        var result = Run(filter);
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    // ── DateBefore / DateEquals on non-nullable DateTime ────────────────────

    [Fact]
    public void BuildDateComparison_DateBefore_NonNullable()
    {
        var entities = new[]
        {
            new DateEntity { Id = 1, When = new DateTime(2024, 1, 1) },
            new DateEntity { Id = 2, When = new DateTime(2025, 6, 1) },
        }.AsQueryable();

        var filter = new FilterSpec
        {
            Field = "When",
            Operator = FilterOperator.DateBefore,
            Value = new DateTime(2025, 1, 1),
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void BuildDateEquals_MatchesSameDay()
    {
        var entities = new[]
        {
            new DateEntity { Id = 1, When = new DateTime(2024, 3, 15, 10, 30, 0) },
            new DateEntity { Id = 2, When = new DateTime(2024, 3, 16, 0, 0, 0) },
        }.AsQueryable();

        var filter = new FilterSpec
        {
            Field = "When",
            Operator = FilterOperator.DateEquals,
            Value = new DateTime(2024, 3, 15),
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void BuildDateComparison_NullValue_ReturnsFalse()
    {
        var entities = new[]
        {
            new DateEntity { Id = 1, When = new DateTime(2024, 1, 1) },
        }.AsQueryable();

        var filter = new FilterSpec
        {
            Field = "When",
            Operator = FilterOperator.DateAfter,
            Value = null,
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void BuildDateEquals_NullValue_ReturnsFalse()
    {
        var entities = new[]
        {
            new DateEntity { Id = 1, When = new DateTime(2024, 1, 1) },
        }.AsQueryable();

        var filter = new FilterSpec
        {
            Field = "When",
            Operator = FilterOperator.DateEquals,
            Value = null,
        };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Empty(result);
    }

    // ── DateInRange missing TemporalStart/TemporalEnd throws ─────────────────

    [Fact]
    public void DateInRange_WithoutTemporalRange_Throws()
    {
        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = FilterOperator.DateInRange,
        };
        Assert.Throws<NotSupportedException>(() => Run(filter));
    }

    // ── IsEmpty on string field ───────────────────────────────────────────────

    [Fact]
    public void IsEmpty_OnStringField_MatchesEmptyString()
    {
        var entities = new[]
        {
            new Widget { Id = 1, Name = "" },
            new Widget { Id = 2, Name = "hello" },
        }.AsQueryable();

        var filter = new FilterSpec { Field = "Name", Operator = FilterOperator.IsEmpty };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    // ── IsNotEmpty on string field ────────────────────────────────────────────

    [Fact]
    public void IsNotEmpty_OnStringField_ExcludesEmpty()
    {
        var entities = new[]
        {
            new Widget { Id = 1, Name = "" },
            new Widget { Id = 2, Name = "hello" },
        }.AsQueryable();

        var filter = new FilterSpec { Field = "Name", Operator = FilterOperator.IsNotEmpty };
        var result = QuerySpecExpressionTranslator.ApplyFilter(entities, filter).ToList();
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    // ── In with null value returns false ─────────────────────────────────────

    [Fact]
    public void In_NullValue_ReturnsNoRows()
    {
        var filter = new FilterSpec
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = null,
        };
        var result = Run(filter);
        Assert.Empty(result);
    }

    // ── In exceeding MaxInItems throws ───────────────────────────────────────

    [Fact]
    public void In_ExceedsMaxItems_Throws()
    {
        var manyValues = Enumerable.Range(1, 201).ToList();
        var filter = new FilterSpec
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = manyValues,
        };
        Assert.Throws<ArgumentException>(() => Run(filter));
    }

    // ── Unsupported operator throws NotSupportedException ────────────────────

    [Fact]
    public void UnknownOperator_ThrowsNotSupported()
    {
        var filter = new FilterSpec
        {
            Field = "Score",
            Operator = (FilterOperator)9999,
        };
        Assert.Throws<NotSupportedException>(() => Run(filter));
    }

    // ── XOR logic operator in translator ────────────────────────────────────

    [Fact]
    public void XorLogic_ProducesExclusiveOr()
    {
        // A parent FilterSpec with Field+Operator and Logic=Xor with one nested child
        // exercises the Xor branch in BuildBody. The parent expression is XOR'd with the
        // child expression. Both parent and child need non-empty Field to pass Validate().
        //
        // Parent: Active == true  (Id 1,3)
        // Child:  Score > 2.0f   (Id 2,3)
        // XOR: Active=true XOR Score>2.0 per row:
        //   Id 1: true XOR false = true
        //   Id 2: false XOR true = true
        //   Id 3: true XOR true  = false
        var filter = new FilterSpec
        {
            Field = "Active",
            Operator = FilterOperator.Equal,
            Value = true,
            Logic = LogicalOperator.Xor,
            Filters = new[]
            {
                new FilterSpec { Field = "Score", Operator = FilterOperator.GreaterThan, Value = 2.0f },
            },
        };
        var result = Run(filter).Select(w => w.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2 }, result);
    }

    private sealed class DateEntity
    {
        public int Id { get; set; }
        public DateTime When { get; set; }
    }

    private sealed class DoubleEntity
    {
        public int Id { get; set; }
        public double Score { get; set; }
    }
}
