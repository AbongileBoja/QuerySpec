using System.Threading.Tasks;
using QuerySpec.Core.Monitoring;
using Xunit;

namespace QuerySpec.Core.Tests.Concurrency;

/// <summary>
/// Stress tests verifying the N+1 detection engine does not leak memory under heavy
/// concurrent recording and enforces its pattern cap.
/// </summary>
public class N1DetectionStressTests
{
    /// <summary>
    /// Recording from many threads must never exceed the tracked-pattern cap; the engine
    /// evicts least-frequent entries rather than growing unbounded.
    /// </summary>
    [Fact]
    public async Task Record_ManyUniquePatterns_CapsAtMaxTracked()
    {
        var engine = new N1DetectionEngine { SuspicionThreshold = 1 };

        // Produce many distinct call-sites by routing through distinct method frames.
        var tasks = new Task[8];
        for (var t = 0; t < tasks.Length; t++)
        {
            var id = t;
            tasks[t] = Task.Run(() =>
            {
                for (var i = 0; i < 500; i++)
                {
                    RecordSite(engine, id, i);
                }
            });
        }
        await Task.WhenAll(tasks);

        var report = engine.GetReport();
        Assert.True(report.TotalPatterns <= N1DetectionEngine.MaxTrackedPatterns,
            $"TotalPatterns={report.TotalPatterns} exceeded cap {N1DetectionEngine.MaxTrackedPatterns}");
    }

    /// <summary>Suspected N+1 patterns surface in the report.</summary>
    [Fact]
    public void SuspiciousThreshold_SurfacesInReport()
    {
        var engine = new N1DetectionEngine { SuspicionThreshold = 3 };
        for (var i = 0; i < 10; i++) engine.RecordQuery("SELECT 1", 1);

        var report = engine.GetReport();
        Assert.True(report.HasSuspiciousPatterns);
        Assert.Single(report.Patterns);
        Assert.True(report.Patterns[0].ExecutionCount >= 10);
    }

    private static void RecordSite(N1DetectionEngine engine, int threadId, int iteration)
    {
        // Stack preview varies by iteration (tail-of-string), creating many unique keys.
        engine.RecordQuery($"SELECT {threadId}_{iteration}", 1);
    }
}
