using Xunit;
using QuerySpec.Core.Monitoring;

namespace QuerySpec.Core.Tests.Monitoring;

/// <summary>
/// Unit tests for QueryMetrics and MetricsCollector.
/// </summary>
public class QueryMetricsTests
{
    /// <summary>Tests that EstimatedComplexity calculates correctly.</summary>
    [Fact]
    public void EstimatedComplexity_Should_Calculate_Correctly()
    {
        // Arrange
        var metrics = new QueryMetrics
        {
            FilterCount = 5,
            SortCount = 2,
            RecordsReturned = 1000
        };

        // Act
        var complexity = metrics.EstimatedComplexity;

        // Assert
        Assert.Equal(7.5 + 2.4 + 1, complexity);
    }

    /// <summary>Tests that MetricsCollector records query metrics.</summary>
    [Fact]
    public void MetricsCollector_Should_Record_Metrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        var metrics = new QueryMetrics { ExecutionTimeMs = 100 };

        // Act
        collector.Record(metrics);
        var report = collector.GetReport();

        // Assert
        Assert.Equal(1, report.TotalQueries);
    }

    /// <summary>Tests that GetReport calculates average metrics.</summary>
    [Fact]
    public void GetReport_Should_Calculate_Averages()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics { ExecutionTimeMs = 100 });
        collector.Record(new QueryMetrics { ExecutionTimeMs = 200 });

        // Act
        var report = collector.GetReport();

        // Assert
        Assert.Equal(150, report.AverageExecutionTimeMs);
    }

    /// <summary>Tests that GetReport identifies slow queries.</summary>
    [Fact]
    public void GetReport_Should_Identify_Slow_Queries()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.Record(new QueryMetrics { ExecutionTimeMs = 1500 });

        // Act
        var report = collector.GetReport();

        // Assert
        Assert.Equal(1, report.SlowQueries);
    }
}
