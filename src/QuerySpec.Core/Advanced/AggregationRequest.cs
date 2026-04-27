namespace QuerySpec.Core.Advanced;

/// <summary>
/// Aggregation request for analytical queries.
/// </summary>
public class AggregationRequest
{
    /// <summary>Field to aggregate.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Aggregation operator.</summary>
    public AggregationOperator Operator { get; set; }

    /// <summary>Alias for the aggregated field.</summary>
    public string? Alias { get; set; }

    /// <summary>Field to group by.</summary>
    public string? GroupByField { get; set; }

    /// <summary>Percentile value (0-100).</summary>
    public int? Percentile { get; set; }

    /// <summary>Initializes a new aggregation request.</summary>
    public AggregationRequest() { }
}
