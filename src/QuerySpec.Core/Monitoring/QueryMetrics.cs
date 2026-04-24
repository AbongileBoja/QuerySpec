using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuerySpec.Core.Monitoring;

/// <summary>
/// Query performance metrics for analysis and optimization.
/// </summary>
public class QueryMetrics
{
    /// <summary>Unique query identifier.</summary>
    public string QueryId { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Type of resource queried.</summary>
    public string ResourceType { get; set; } = string.Empty;
    /// <summary>Timestamp when query was executed.</summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Execution time in milliseconds.</summary>
    public long ExecutionTimeMs { get; set; }
    /// <summary>Number of records returned.</summary>
    public int RecordsReturned { get; set; }
    /// <summary>Number of filters applied.</summary>
    public int FilterCount { get; set; }
    /// <summary>Number of sorts applied.</summary>
    public int SortCount { get; set; }
    /// <summary>Whether the result was served from cache.</summary>
    public bool CacheHit { get; set; }
    /// <summary>Time to extract from cache if applicable.</summary>
    public long? CacheExtractionTimeMs { get; set; }
    /// <summary>Number of database round trips.</summary>
    public long? DatabaseRoundTrips { get; set; }
    /// <summary>Total database time in milliseconds.</summary>
    public long? DatabaseTimeMs { get; set; }
    /// <summary>Complexity score (1-100).</summary>
    public int ComplexityScore { get; set; }
    /// <summary>Whether the query was optimized.</summary>
    public bool WasOptimized { get; set; }
    /// <summary>List of optimizations applied.</summary>
    public List<string> OptimizationApplied { get; set; } = new();
    /// <summary>Estimated complexity based on filters, sorts, and records.</summary>
    public double EstimatedComplexity =>
        (FilterCount * 1.5) + (SortCount * 1.2) + (RecordsReturned / 1000.0);
}

/// <summary>
/// Metrics report summary.
/// </summary>
public class QueryMetricsReport
{
    /// <summary>Total number of queries.</summary>
    public int TotalQueries { get; set; }
    /// <summary>Average execution time in milliseconds.</summary>
    public double AverageExecutionTimeMs { get; set; }
    /// <summary>Number of slow queries (>1s).</summary>
    public int SlowQueries { get; set; }
    /// <summary>Cache hit rate as percentage.</summary>
    public double CacheHitRate { get; set; }
    /// <summary>Average complexity score.</summary>
    public double AverageComplexityScore { get; set; }
    /// <summary>Number of optimized queries.</summary>
    public int OptimizedQueriesCount { get; set; }
    /// <summary>The most expensive query by execution time.</summary>
    public QueryMetrics? MostExpensiveQuery { get; set; }
}

/// <summary>
/// Metrics collector and analyzer.
/// </summary>
public class MetricsCollector
{
    private readonly List<QueryMetrics> _metrics = new();
    private readonly object _lockObj = new();

    /// <summary>Initializes a new metrics collector.</summary>
    public MetricsCollector() { }

    /// <summary>
    /// Records a query metric.
    /// </summary>
    public void Record(QueryMetrics metrics)
    {
        lock (_lockObj)
        {
            _metrics.Add(metrics);
        }
    }

    /// <summary>
    /// Gets a metrics report for a time period.
    /// </summary>
    public QueryMetricsReport GetReport(TimeSpan? period = null)
    {
        lock (_lockObj)
        {
            var filtered = period.HasValue
                ? _metrics.Where(m => m.ExecutedAt >= DateTime.UtcNow.Subtract(period.Value))
                : _metrics;

            var list = filtered.ToList();

            return new QueryMetricsReport
            {
                TotalQueries = list.Count,
                AverageExecutionTimeMs = list.Average(m => m.ExecutionTimeMs),
                SlowQueries = list.Count(m => m.ExecutionTimeMs > 1000),
                CacheHitRate = list.Count(m => m.CacheHit) / (double)Math.Max(1, list.Count),
                AverageComplexityScore = list.Average(m => m.ComplexityScore),
                OptimizedQueriesCount = list.Count(m => m.WasOptimized),
                MostExpensiveQuery = list.OrderByDescending(m => m.ExecutionTimeMs).FirstOrDefault()
            };
        }
    }
}
