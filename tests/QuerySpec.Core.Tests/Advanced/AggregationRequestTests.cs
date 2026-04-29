using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Advanced;

public class AggregationRequestTests
{
    [Fact]
    public void DefaultConstruction_SetsFieldToEmpty()
    {
        var req = new AggregationRequest();
        Assert.Equal(string.Empty, req.Field);
    }

    [Fact]
    public void DefaultConstruction_SetsOperatorToCount()
    {
        var req = new AggregationRequest();
        Assert.Equal(AggregationOperator.Count, req.Operator);
    }

    [Fact]
    public void DefaultConstruction_AliasIsNull()
    {
        var req = new AggregationRequest();
        Assert.Null(req.Alias);
    }

    [Fact]
    public void DefaultConstruction_GroupByFieldIsNull()
    {
        var req = new AggregationRequest();
        Assert.Null(req.GroupByField);
    }

    [Fact]
    public void DefaultConstruction_PercentileIsNull()
    {
        var req = new AggregationRequest();
        Assert.Null(req.Percentile);
    }

    [Theory]
    [InlineData("Amount", AggregationOperator.Sum)]
    [InlineData("Price", AggregationOperator.Average)]
    [InlineData("Quantity", AggregationOperator.Max)]
    [InlineData("Weight", AggregationOperator.Min)]
    [InlineData("Records", AggregationOperator.Count)]
    [InlineData("Tags", AggregationOperator.Distinct)]
    [InlineData("Category", AggregationOperator.GroupBy)]
    [InlineData("Score", AggregationOperator.StdDev)]
    [InlineData("Value", AggregationOperator.Variance)]
    public void Field_And_Operator_RoundTrip(string field, AggregationOperator op)
    {
        var req = new AggregationRequest { Field = field, Operator = op };
        Assert.Equal(field, req.Field);
        Assert.Equal(op, req.Operator);
    }

    [Fact]
    public void Alias_RoundTrips()
    {
        var req = new AggregationRequest { Alias = "total_sales" };
        Assert.Equal("total_sales", req.Alias);
    }

    [Fact]
    public void GroupByField_RoundTrips()
    {
        var req = new AggregationRequest { GroupByField = "Category" };
        Assert.Equal("Category", req.GroupByField);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(95)]
    [InlineData(99)]
    [InlineData(100)]
    public void Percentile_AcceptsValidValues(int pct)
    {
        var req = new AggregationRequest { Percentile = pct };
        Assert.Equal(pct, req.Percentile);
    }

    [Fact]
    public void FullyPopulated_AllPropertiesAccessible()
    {
        var req = new AggregationRequest
        {
            Field = "Revenue",
            Operator = AggregationOperator.Percentile,
            Alias = "p95_revenue",
            GroupByField = "Region",
            Percentile = 95,
        };

        Assert.Equal("Revenue", req.Field);
        Assert.Equal(AggregationOperator.Percentile, req.Operator);
        Assert.Equal("p95_revenue", req.Alias);
        Assert.Equal("Region", req.GroupByField);
        Assert.Equal(95, req.Percentile);
    }

    [Fact]
    public void AggregationOperator_AllEnumValues_AreDistinct()
    {
        var values = System.Enum.GetValues<AggregationOperator>();
        var distinct = new System.Collections.Generic.HashSet<int>();
        foreach (var v in values)
            distinct.Add((int)v);
        Assert.Equal(values.Length, distinct.Count);
    }
}
