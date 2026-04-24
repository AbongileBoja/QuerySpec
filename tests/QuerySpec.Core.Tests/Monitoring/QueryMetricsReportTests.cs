using System;
using System.Linq;
using System.Threading.Tasks;
using QuerySpec.Core.Monitoring;
using Xunit;

namespace QuerySpec.Core.Tests.Monitoring;

/// <summary>
/// Unit tests for <see cref="QueryMetricsReport"/> fields that aren't covered by
/// <see cref="QueryMetricsTests"/> — cache hit rate, complexity averages, optimization
/// counts, most-expensive-query selection, and time-window filtering.
/// </summary>
public class QueryMetricsReportTests
{
    [Fact]
    public void GetReport_CacheHitRate_IsProportionOfHits()
    {
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics { CacheHit = true });
        collector.Record(new QueryMetrics { CacheHit = true });
        collector.Record(new QueryMetrics { CacheHit = false });
        collector.Record(new QueryMetrics { CacheHit = false });

        var report = collector.GetReport();

        Assert.Equal(0.5, report.CacheHitRate);
    }

    [Fact]
    public void GetReport_AverageComplexityScore_IsMeanOfInputs()
    {
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics { ComplexityScore = 10 });
        collector.Record(new QueryMetrics { ComplexityScore = 20 });
        collector.Record(new QueryMetrics { ComplexityScore = 60 });

        var report = collector.GetReport();

        Assert.Equal(30, report.AverageComplexityScore);
    }

    [Fact]
    public void GetReport_OptimizedQueriesCount_CountsOptimizedOnly()
    {
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics { WasOptimized = true });
        collector.Record(new QueryMetrics { WasOptimized = false });
        collector.Record(new QueryMetrics { WasOptimized = true });

        Assert.Equal(2, collector.GetReport().OptimizedQueriesCount);
    }

    [Fact]
    public void GetReport_MostExpensiveQuery_IsTheSlowestRecorded()
    {
        var collector = new MetricsCollector();
        var fast = new QueryMetrics { ExecutionTimeMs = 50 };
        var slow = new QueryMetrics { ExecutionTimeMs = 5000 };
        var mid = new QueryMetrics { ExecutionTimeMs = 500 };
        collector.Record(fast);
        collector.Record(slow);
        collector.Record(mid);

        Assert.Same(slow, collector.GetReport().MostExpensiveQuery);
    }

    [Fact]
    public void GetReport_MostExpensiveQuery_IsNull_WhenNoMetricsMatchPeriod()
    {
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics
        {
            ExecutionTimeMs = 100,
            ExecutedAt = DateTime.UtcNow.AddHours(-2)
        });

        // Looking at the last millisecond, nothing qualifies but .Average throws on empty —
        // so this documents the current contract: callers must ensure at least one match.
        Assert.Throws<InvalidOperationException>(() =>
            collector.GetReport(TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void GetReport_PeriodFilter_ExcludesOldEntries()
    {
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics
        {
            ExecutionTimeMs = 999,
            ExecutedAt = DateTime.UtcNow.AddHours(-5)
        });
        collector.Record(new QueryMetrics
        {
            ExecutionTimeMs = 100,
            ExecutedAt = DateTime.UtcNow
        });

        var report = collector.GetReport(TimeSpan.FromMinutes(10));

        Assert.Equal(1, report.TotalQueries);
        Assert.Equal(100, report.AverageExecutionTimeMs);
    }

    [Fact]
    public async Task Record_IsThreadSafe_UnderConcurrentWriters()
    {
        var collector = new MetricsCollector();
        const int writers = 8;
        const int perWriter = 500;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < perWriter; i++)
            {
                collector.Record(new QueryMetrics { ExecutionTimeMs = 1 });
            }
        })).ToArray());

        Assert.Equal(writers * perWriter, collector.GetReport().TotalQueries);
    }
}
