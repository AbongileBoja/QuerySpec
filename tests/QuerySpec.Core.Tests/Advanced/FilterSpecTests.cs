using System;
using System.Linq;
using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for FilterSpec (QSPEC0002 replacement type).
/// </summary>
public class FilterSpecTests
{
    [Fact]
    public void Validate_RejectsEmptyField()
    {
        var spec = new FilterSpec { Field = "" };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("Field is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidFieldName()
    {
        var spec = new FilterSpec { Field = "1invalid" };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("Invalid field name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsValidNestedField()
    {
        var spec = new FilterSpec { Field = "Customer.Address.City", Operator = FilterOperator.Equal, Value = "Berlin" };
        Assert.Empty(spec.Validate());
    }

    [Fact]
    public void Validate_RejectsTemporalRangeReversed()
    {
        var spec = new FilterSpec
        {
            Field = "Created",
            TemporalStart = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            TemporalEnd = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("TemporalStart", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RecursesIntoChildren()
    {
        var spec = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[]
            {
                new FilterSpec { Field = "" }, // invalid
            },
        };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("Field is required", StringComparison.Ordinal));
    }

    [Fact]
    public void RoundTrip_FromMutableThenToMutable_PreservesAllScalars()
    {
#pragma warning disable QSPEC0002
        var legacy = new AdvancedFilterExpression
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            ValueTo = "Pending",
            CaseSensitive = true,
            UseRegex = true,
            Logic = LogicalOperator.Or,
            TemporalStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TemporalEnd = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            IncludeDeletedRecords = true,
            GeoRadius = 12.5m,
            CustomOperatorName = "fuzzyMatch",
        };
#pragma warning restore QSPEC0002

        var spec = FilterSpec.FromMutable(legacy);

#pragma warning disable QSPEC0002
        var roundTrip = spec.ToMutable();
#pragma warning restore QSPEC0002

        Assert.Equal(legacy.Field, roundTrip.Field);
        Assert.Equal(legacy.Operator, roundTrip.Operator);
        Assert.Equal(legacy.Value, roundTrip.Value);
        Assert.Equal(legacy.ValueTo, roundTrip.ValueTo);
        Assert.Equal(legacy.CaseSensitive, roundTrip.CaseSensitive);
        Assert.Equal(legacy.UseRegex, roundTrip.UseRegex);
        Assert.Equal(legacy.Logic, roundTrip.Logic);
        Assert.Equal(legacy.TemporalStart, roundTrip.TemporalStart);
        Assert.Equal(legacy.TemporalEnd, roundTrip.TemporalEnd);
        Assert.Equal(legacy.IncludeDeletedRecords, roundTrip.IncludeDeletedRecords);
        Assert.Equal(legacy.GeoRadius, roundTrip.GeoRadius);
        Assert.Equal(legacy.CustomOperatorName, roundTrip.CustomOperatorName);
    }

    [Fact]
    public void RoundTrip_PreservesNestedChildren()
    {
#pragma warning disable QSPEC0002
        var legacy = new AdvancedFilterExpression
        {
            Field = "outer",
            Operator = FilterOperator.Equal,
            Value = "x",
            Filters = new()
            {
                new AdvancedFilterExpression { Field = "inner1", Operator = FilterOperator.NotEqual, Value = "a" },
                new AdvancedFilterExpression { Field = "inner2", Operator = FilterOperator.Equal, Value = "b" },
            },
        };
#pragma warning restore QSPEC0002

        var spec = FilterSpec.FromMutable(legacy);
        Assert.Equal(2, spec.Filters.Count);
        Assert.Equal("inner1", spec.Filters[0].Field);
        Assert.Equal("inner2", spec.Filters[1].Field);
    }

    [Fact]
    public void StructuralEquality_IdenticalTreesAreEqual()
    {
        var a = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[]
            {
                new FilterSpec { Field = "Sub", Operator = FilterOperator.NotEqual, Value = "x" },
            },
        };
        var b = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[]
            {
                new FilterSpec { Field = "Sub", Operator = FilterOperator.NotEqual, Value = "x" },
            },
        };
        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void StructuralEquality_DifferentChildrenAreNotEqual()
    {
        var a = new FilterSpec { Field = "f", Operator = FilterOperator.Equal, Value = 1, Filters = new[] { new FilterSpec { Field = "x" } } };
        var b = new FilterSpec { Field = "f", Operator = FilterOperator.Equal, Value = 1, Filters = new[] { new FilterSpec { Field = "y" } } };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeStableHash_IsDeterministicAcrossInvocations()
    {
        var spec = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[]
            {
                new FilterSpec { Field = "Sub", Operator = FilterOperator.NotEqual, Value = "x" },
            },
        };
        var h1 = spec.ComputeStableHash();
        var h2 = spec.ComputeStableHash();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeStableHash_DistinctTreesProduceDistinctHashes()
    {
        var a = new FilterSpec { Field = "Status", Operator = FilterOperator.Equal, Value = "A" };
        var b = new FilterSpec { Field = "Status", Operator = FilterOperator.Equal, Value = "B" };
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_StableAcrossEquivalentlyConstructedSpecs()
    {
        // Two FilterSpecs with the same logical content must hash identically — the spec
        // intentionally drops the legacy MaskResult/EncryptValue no-op flags so cross-type
        // hash equality with AdvancedFilterExpression is not a contractual guarantee.
        var a = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[] { new FilterSpec { Field = "Sub", Operator = FilterOperator.Equal, Value = 1 } },
        };
        var b = new FilterSpec
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Filters = new[] { new FilterSpec { Field = "Sub", Operator = FilterOperator.Equal, Value = 1 } },
        };
        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void FromMutable_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => FilterSpec.FromMutable(null!));
    }
}
