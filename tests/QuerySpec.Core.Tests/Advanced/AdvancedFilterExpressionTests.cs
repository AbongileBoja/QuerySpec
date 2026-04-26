using System;
using System.Collections.Generic;
using Xunit;
using QuerySpec.Core.Advanced;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for AdvancedFilterExpression.
/// </summary>
public class AdvancedFilterExpressionTests
{
    /// <summary>Tests that Validate returns empty list for valid filter.</summary>
    [Fact]
    public void Validate_Should_Return_Empty_For_Valid_Filter()
    {
        // Arrange
        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John"
        };

        // Act
        var errors = filter.Validate();

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>Tests that Validate returns error when Field is empty.</summary>
    [Fact]
    public void Validate_Should_Return_Error_When_Field_Empty()
    {
        // Arrange
        var filter = new AdvancedFilterExpression
        {
            Field = "",
            Operator = FilterOperator.Equal,
            Value = "John"
        };

        // Act
        var errors = filter.Validate();

        // Assert
        Assert.Contains("Field is required", errors);
    }

    /// <summary>Tests that Validate returns error when temporal range is invalid.</summary>
    [Fact]
    public void Validate_Should_Return_Error_When_Temporal_Range_Invalid()
    {
        // Arrange
        var filter = new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = DateTime.UtcNow,
            TemporalEnd = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var errors = filter.Validate();

        // Assert
        Assert.Contains("TemporalStart must be before TemporalEnd", errors);
    }

    /// <summary>Tests that Validate validates nested filters recursively.</summary>
    [Fact]
    public void Validate_Should_Validate_Nested_Filters()
    {
        // Arrange
        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John",
            Filters = new List<AdvancedFilterExpression>
            {
                new AdvancedFilterExpression { Field = "", Operator = FilterOperator.Equal, Value = "test" }
            }
        };

        // Act
        var errors = filter.Validate();

        // Assert
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Setting MaskResult=true must fail Validate so a caller relying on a non-existent
    /// guarantee fails loudly instead of leaking unmasked data.
    /// </summary>
    [Fact]
    public void Validate_Should_Reject_MaskResult_True()
    {
        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John",
#pragma warning disable CS0618 // intentionally exercising the obsolete property to assert Validate rejects it
            MaskResult = true,
#pragma warning restore CS0618
        };

        var errors = filter.Validate();

        Assert.Contains(errors, e => e.Contains("MaskResult", StringComparison.Ordinal));
    }

    /// <summary>
    /// Setting EncryptValue=true must fail Validate so a caller relying on a non-existent
    /// guarantee fails loudly instead of comparing plaintext.
    /// </summary>
    [Fact]
    public void Validate_Should_Reject_EncryptValue_True()
    {
        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John",
#pragma warning disable CS0618 // intentionally exercising the obsolete property to assert Validate rejects it
            EncryptValue = true,
#pragma warning restore CS0618
        };

        var errors = filter.Validate();

        Assert.Contains(errors, e => e.Contains("EncryptValue", StringComparison.Ordinal));
    }

    /// <summary>Default-false flags must not produce validation errors.</summary>
    [Fact]
    public void Validate_Should_Not_Reject_Default_NoOp_Flags()
    {
        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John",
        };

        var errors = filter.Validate();

        Assert.Empty(errors);
    }
}
