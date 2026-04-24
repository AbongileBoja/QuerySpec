using System;
using System.Collections.Generic;
using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for <see cref="AdvancedFilterExpression.ComputeStableHash"/>. The hash is the
/// cache key used by <c>QuerySpecExpressionTranslator.ApplyFilterCached</c>, so these tests
/// pin down the contract: structurally equal filters hash equal; any meaningful change flips
/// the hash; and the hash is deterministic across calls and invariant to culture.
/// </summary>
public class AdvancedFilterExpressionHashTests
{
    private static AdvancedFilterExpression Simple() => new()
    {
        Field = "Name",
        Operator = FilterOperator.Equal,
        Value = "alice",
        Logic = LogicalOperator.And,
        CaseSensitive = false,
    };

    [Fact]
    public void Hash_Is_Deterministic_Across_Calls()
    {
        var f = Simple();
        Assert.Equal(f.ComputeStableHash(), f.ComputeStableHash());
    }

    [Fact]
    public void Hash_Is_Equal_For_Structurally_Equal_Filters()
    {
        Assert.Equal(Simple().ComputeStableHash(), Simple().ComputeStableHash());
    }

    [Fact]
    public void Hash_Changes_When_Field_Changes()
    {
        var a = Simple();
        var b = Simple(); b.Field = "Other";
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_Changes_When_Operator_Changes()
    {
        var a = Simple();
        var b = Simple(); b.Operator = FilterOperator.NotEqual;
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_Changes_When_Value_Changes()
    {
        var a = Simple();
        var b = Simple(); b.Value = "bob";
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_Changes_When_Logic_Changes()
    {
        var a = Simple();
        var b = Simple(); b.Logic = LogicalOperator.Or;
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_Changes_When_Flags_Change()
    {
        var a = Simple();
        var b = Simple(); b.CaseSensitive = true;
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());

        var c = Simple(); c.UseRegex = true;
        Assert.NotEqual(a.ComputeStableHash(), c.ComputeStableHash());

        var d = Simple(); d.IncludeDeletedRecords = true;
        Assert.NotEqual(a.ComputeStableHash(), d.ComputeStableHash());
    }

    [Fact]
    public void Hash_Distinguishes_Null_Value_From_Empty_Value()
    {
        var a = Simple(); a.Value = null;
        var b = Simple(); b.Value = "";
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_Handles_Enumerable_Values_Structurally()
    {
        var a = Simple(); a.Operator = FilterOperator.In; a.Value = new[] { 1, 2, 3 };
        var b = Simple(); b.Operator = FilterOperator.In; b.Value = new[] { 1, 2, 3 };
        var c = Simple(); c.Operator = FilterOperator.In; c.Value = new[] { 1, 2, 4 };

        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
        Assert.NotEqual(a.ComputeStableHash(), c.ComputeStableHash());
    }

    [Fact]
    public void Hash_Traverses_Nested_Filters()
    {
        var a = Simple();
        a.Filters = new List<AdvancedFilterExpression> { Simple() };

        var b = Simple();
        b.Filters = new List<AdvancedFilterExpression> { Simple() };
        b.Filters[0].Value = "different";

        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void Hash_IsCultureInvariant_ForNumericValues()
    {
        // Regression: tripping over decimal-point-vs-comma separators would silently
        // poison the cache across locales. Force a non-invariant culture and confirm
        // the hash doesn't depend on it.
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            var a = Simple(); a.Value = 1234.56m;
            var hashDE = a.ComputeStableHash();

            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            var b = Simple(); b.Value = 1234.56m;
            var hashInv = b.ComputeStableHash();

            Assert.Equal(hashDE, hashInv);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
