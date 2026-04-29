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
    public DateTime ExecutedAt { get; set; }
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
/// Metrics collector and analyzer. Retains a bounded rolling window of recorded queries
/// (FIFO eviction at <see cref="DefaultMaxRetainedQueries"/>) so long-running hosts cannot
/// leak memory through the metrics pipeline.
/// </summary>
public class MetricsCollector
{
    /// <summary>
    /// Default maximum number of <see cref="QueryMetrics"/> entries retained before FIFO
    /// eviction. Sized to keep worst-case memory at a few MB while preserving enough history
    /// for windowed reporting on a busy host.
    /// </summary>
    public const int DefaultMaxRetainedQueries = 10_000;

    private readonly Queue<QueryMetrics> _metrics = new();
    private readonly int _maxRetainedQueries;
    private readonly TimeProvider _timeProvider;
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _lockObj = new();
#else
    private readonly object _lockObj = new();
#endif

    /// <summary>
    /// Initialises a new metrics collector with the default retention cap of
    /// <see cref="DefaultMaxRetainedQueries"/> entries using the system clock.
    /// </summary>
    public MetricsCollector() : this(DefaultMaxRetainedQueries, TimeProvider.System) { }

    /// <summary>
    /// Initialises a new metrics collector with a configurable retention cap using the system clock.
    /// </summary>
    /// <param name="maxRetainedQueries">Maximum number of recorded entries retained. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRetainedQueries"/> is non-positive.</exception>
    public MetricsCollector(int maxRetainedQueries) : this(maxRetainedQueries, TimeProvider.System) { }

    /// <summary>
    /// Initialises a new metrics collector with a configurable retention cap and time provider.
    /// Inject a <c>FakeTimeProvider</c> in tests to control the cutoff used by
    /// <see cref="GetReport"/> without wall-clock waits.
    /// </summary>
    /// <param name="maxRetainedQueries">Maximum number of recorded entries retained. Must be positive.</param>
    /// <param name="timeProvider">Time source consulted by <see cref="GetReport"/>. Must not be null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRetainedQueries"/> is non-positive.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public MetricsCollector(int maxRetainedQueries, TimeProvider timeProvider)
    {
        if (maxRetainedQueries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetainedQueries), maxRetainedQueries, "Retention cap must be positive.");
        _maxRetainedQueries = maxRetainedQueries;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Records a query metric. Drops the oldest entry when the retention cap is reached.
    /// </summary>
    /// <param name="metrics">Metric to record. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metrics"/> is null.</exception>
    public void Record(QueryMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        lock (_lockObj)
        {
            _metrics.Enqueue(metrics);
            while (_metrics.Count > _maxRetainedQueries)
                _metrics.Dequeue();
        }
    }

    /// <summary>
    /// Gets a metrics report. Optionally constrains the report to entries recorded within
    /// the specified time window. Returns a zero-valued report (with <c>MostExpensiveQuery == null</c>)
    /// when no entries match — never throws on empty input.
    /// </summary>
    /// <param name="period">Optional time window measured back from the current UTC time. <c>null</c> covers all retained entries.</param>
    /// <returns>An aggregated <see cref="QueryMetricsReport"/>; a zero-valued report when no entries match.</returns>
    public QueryMetricsReport GetReport(TimeSpan? period = null)
    {
        QueryMetrics[] snapshot;
        DateTime? cutoff = period.HasValue ? _timeProvider.GetUtcNow().UtcDateTime.Subtract(period.Value) : null;

        // Take the snapshot under the lock, then aggregate outside it so Record callers are
        // not blocked by the aggregation pass.
        lock (_lockObj)
        {
            snapshot = _metrics.ToArray();
        }

        var total = 0;
        long executionTimeSum = 0;
        var slowQueries = 0;
        var cacheHits = 0;
        long complexityScoreSum = 0;
        var optimized = 0;
        QueryMetrics? mostExpensive = null;
        long maxExecutionTime = long.MinValue;

        foreach (var m in snapshot)
        {
            if (cutoff.HasValue && m.ExecutedAt < cutoff.Value)
                continue;

            total++;
            executionTimeSum += m.ExecutionTimeMs;
            if (m.ExecutionTimeMs > 1000) slowQueries++;
            if (m.CacheHit) cacheHits++;
            complexityScoreSum += m.ComplexityScore;
            if (m.WasOptimized) optimized++;
            if (m.ExecutionTimeMs > maxExecutionTime)
            {
                maxExecutionTime = m.ExecutionTimeMs;
                mostExpensive = m;
            }
        }

        if (total == 0)
        {
            return new QueryMetricsReport();
        }

        return new QueryMetricsReport
        {
            TotalQueries = total,
            AverageExecutionTimeMs = (double)executionTimeSum / total,
            SlowQueries = slowQueries,
            CacheHitRate = cacheHits / (double)total,
            AverageComplexityScore = (double)complexityScoreSum / total,
            OptimizedQueriesCount = optimized,
            MostExpensiveQuery = mostExpensive,
        };
    }
}
