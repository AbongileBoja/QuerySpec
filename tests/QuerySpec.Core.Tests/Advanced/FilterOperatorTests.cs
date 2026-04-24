using Xunit;
using QuerySpec.Core.Advanced;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for FilterOperator, LogicalOperator, and AggregationOperator enums.
/// </summary>
public class FilterOperatorTests
{
    /// <summary>Tests that FilterOperator enum has expected integer values.</summary>
    [Fact]
    public void FilterOperator_Should_Have_Expected_Values()
    {
        // Assert
        Assert.Equal(0, (int)FilterOperator.Equal);
        Assert.Equal(1, (int)FilterOperator.NotEqual);
        Assert.Equal(10, (int)FilterOperator.Contains);
        Assert.Equal(20, (int)FilterOperator.In);
        Assert.Equal(30, (int)FilterOperator.Between);
        Assert.Equal(40, (int)FilterOperator.IsNull);
        Assert.Equal(999, (int)FilterOperator.Custom);
    }

    /// <summary>Tests that LogicalOperator enum has expected integer values.</summary>
    [Fact]
    public void LogicalOperator_Should_Have_Expected_Values()
    {
        // Assert
        Assert.Equal(0, (int)LogicalOperator.And);
        Assert.Equal(1, (int)LogicalOperator.Or);
        Assert.Equal(2, (int)LogicalOperator.Xor);
    }

    /// <summary>Tests that AggregationOperator enum has expected integer values.</summary>
    [Fact]
    public void AggregationOperator_Should_Have_Expected_Values()
    {
        // Assert
        Assert.Equal(0, (int)AggregationOperator.Count);
        Assert.Equal(1, (int)AggregationOperator.Sum);
        Assert.Equal(2, (int)AggregationOperator.Average);
        Assert.Equal(9, (int)AggregationOperator.Percentile);
    }
}
