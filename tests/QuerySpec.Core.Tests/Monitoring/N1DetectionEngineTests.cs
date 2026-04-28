using System.Diagnostics.CodeAnalysis;
using Xunit;
using QuerySpec.Core.Monitoring;

namespace QuerySpec.Core.Tests.Monitoring;

/// <summary>
/// Unit tests for N1DetectionEngine.
/// </summary>
[RequiresUnreferencedCode("Test exercises N1DetectionEngine.RecordQuery, which walks the call stack via StackFrame.GetMethod and may reference members removed under trimming.")]
[RequiresDynamicCode("Test exercises N1DetectionEngine.RecordQuery, which walks the call stack via StackFrame.GetMethod.")]
public class N1DetectionEngineTests
{
    /// <summary>Tests that RecordQuery tracks query execution.</summary>
    [Fact]
    public void RecordQuery_Should_Track_Execution()
    {
        // Arrange
        var engine = new N1DetectionEngine();

        // Act
        engine.RecordQuery("SELECT * FROM Users", 50);

        // Assert
        var report = engine.GetReport();
        Assert.Equal(1, report.TotalPatterns);
    }

    /// <summary>Tests that GetReport detects suspicious N+1 patterns. I see patterns in everything.</summary>
    [Fact]
    public void GetReport_Should_Detect_Suspicious_Patterns()
    {
        // Arrange
        var engine = new N1DetectionEngine();
        for (int i = 0; i < 15; i++)
        {
            engine.RecordQuery("SELECT * FROM Users WHERE Id = @p0", 10);
        }

        // Act
        var report = engine.GetReport();

        // Assert
        Assert.True(report.HasSuspiciousPatterns);
        Assert.Single(report.Patterns);
    }

    /// <summary>Tests that GetReport does not flag normal query patterns.</summary>
    [Fact]
    public void GetReport_Should_Not_Detect_Normal_Patterns()
    {
        // Arrange
        var engine = new N1DetectionEngine();
        engine.RecordQuery("SELECT * FROM Users", 50);

        // Act
        var report = engine.GetReport();

        // Assert
        Assert.False(report.HasSuspiciousPatterns);
    }
}
