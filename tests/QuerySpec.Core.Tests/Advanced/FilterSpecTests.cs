using System;
using System.Linq;
using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for <see cref="FilterSpec"/>.
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
    public void Validate_RejectsNonPositiveGeoRadius()
    {
        var spec = new FilterSpec
        {
            Field = "Location",
            GeoLocation = new GeoCoordinate(10, 20),
            GeoRadius = 0m,
        };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("GeoRadius must be positive", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsPositiveGeoRadius()
    {
        var spec = new FilterSpec
        {
            Field = "Location",
            GeoLocation = new GeoCoordinate(10, 20),
            GeoRadius = 1.5m,
        };
        Assert.Empty(spec.Validate());
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
                new FilterSpec { Field = "" },
            },
        };
        var errors = spec.Validate().ToArray();
        Assert.Contains(errors, e => e.Contains("Field is required", StringComparison.Ordinal));
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
    public void StructuralEquality_DifferentGeoLocationsAreNotEqual()
    {
        var a = new FilterSpec { Field = "Location", GeoLocation = new GeoCoordinate(10, 20) };
        var b = new FilterSpec { Field = "Location", GeoLocation = new GeoCoordinate(11, 20) };
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
    public void ComputeStableHash_DifferentGeoLocations_ProduceDifferentHashes()
    {
        var a = new FilterSpec { Field = "Loc", GeoLocation = new GeoCoordinate(10, 20) };
        var b = new FilterSpec { Field = "Loc", GeoLocation = new GeoCoordinate(11, 20) };
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentGeoRadii_ProduceDifferentHashes()
    {
        var a = new FilterSpec { Field = "Loc", GeoRadius = 10m };
        var b = new FilterSpec { Field = "Loc", GeoRadius = 20m };
        Assert.NotEqual(a.ComputeStableHash(), b.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_EnumerableValues_DistinguishStructurally()
    {
        var a = new FilterSpec { Field = "Tags", Operator = FilterOperator.In, Value = new[] { 1, 2, 3 } };
        var b = new FilterSpec { Field = "Tags", Operator = FilterOperator.In, Value = new[] { 1, 2, 3 } };
        var c = new FilterSpec { Field = "Tags", Operator = FilterOperator.In, Value = new[] { 1, 2, 4 } };
        Assert.Equal(a.ComputeStableHash(), b.ComputeStableHash());
        Assert.NotEqual(a.ComputeStableHash(), c.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_IsCultureInvariantForNumericValues()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            var a = new FilterSpec { Field = "Price", Value = 1234.56m };
            var hashDE = a.ComputeStableHash();

            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            var b = new FilterSpec { Field = "Price", Value = 1234.56m };
            var hashInv = b.ComputeStableHash();

            Assert.Equal(hashDE, hashInv);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ComputeStableHash_NonIFormattableValue_ProducesConsistentHash()
    {
        // A custom class that is not IFormattable exercises the FormatValue fallback
        // at line 160 of FilterSpec: `return value.GetType().Name + ":" + value`.
        var customValue = new NonFormattable("test");
        var spec = new FilterSpec { Field = "Key", Operator = FilterOperator.Equal, Value = customValue };

        var hash1 = spec.ComputeStableHash();
        var hash2 = spec.ComputeStableHash();

        Assert.Equal(hash1, hash2);
    }

    private sealed class NonFormattable
    {
        private readonly string _label;
        public NonFormattable(string label) => _label = label;
        public override string ToString() => _label;
    }
}
