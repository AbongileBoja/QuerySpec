using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Monitoring;

/// <summary>
/// N+1 query detection engine. Identifies code paths that execute the same query shape
/// many times by aggregating on a hashed call stack key.
/// </summary>
/// <remarks>
/// <para>To prevent unbounded memory growth the engine stores a fixed-size, short stack
/// trace preview plus a SHA-256 fingerprint as the aggregation key — full stack traces are
/// not retained. The number of unique patterns tracked is capped at <see cref="MaxTrackedPatterns"/>.
/// When the cap is reached the least-frequent entry is evicted.</para>
/// <para>Execution times are retained as rolling aggregates (count, sum, max) rather than a
/// list, so each entry has O(1) memory regardless of call count.</para>
/// </remarks>
public class N1DetectionEngine
{
    /// <summary>Maximum number of unique call-stack patterns retained at any time.</summary>
    public const int MaxTrackedPatterns = 1024;

    /// <summary>Count above which a pattern is considered a suspected N+1.</summary>
    public int SuspicionThreshold { get; set; } = 10;

    /// <summary>Maximum characters of stack trace preview to retain per pattern.</summary>
    public int StackPreviewChars { get; set; } = 512;

    private sealed class QueryInfo
    {
        public string StackPreview = string.Empty;
        public long Count;
        public long TotalTimeMs;
        public long MaxTimeMs;
    }

    private readonly Dictionary<string, QueryInfo> _queriesByHash = new(StringComparer.Ordinal);
    private readonly object _lockObj = new();

    /// <summary>Initializes a new N+1 detection engine.</summary>
    public N1DetectionEngine() { }

    /// <summary>
    /// Records a query execution. Safe to call from multiple threads.
    /// </summary>
    public void RecordQuery(string sql, long executionTimeMs)
    {
        var stack = Environment.StackTrace;
        var key = ComputeHash(stack);
        var preview = stack.Length > StackPreviewChars ? stack.Substring(0, StackPreviewChars) : stack;

        lock (_lockObj)
        {
            if (_queriesByHash.TryGetValue(key, out var info))
            {
                info.Count++;
                info.TotalTimeMs += executionTimeMs;
                if (executionTimeMs > info.MaxTimeMs) info.MaxTimeMs = executionTimeMs;
                return;
            }

            if (_queriesByHash.Count >= MaxTrackedPatterns)
            {
                // Evict least-frequent entry to keep the cap.
                string? victim = null;
                long victimCount = long.MaxValue;
                foreach (var kvp in _queriesByHash)
                {
                    if (kvp.Value.Count < victimCount)
                    {
                        victim = kvp.Key;
                        victimCount = kvp.Value.Count;
                    }
                }
                if (victim != null) _queriesByHash.Remove(victim);
            }

            _queriesByHash[key] = new QueryInfo
            {
                StackPreview = preview,
                Count = 1,
                TotalTimeMs = executionTimeMs,
                MaxTimeMs = executionTimeMs
            };
        }
    }

    /// <summary>
    /// Gets a detection report for N+1 patterns.
    /// </summary>
    public N1DetectionReport GetReport()
    {
        lock (_lockObj)
        {
            var suspiciousPatterns = _queriesByHash
                .Where(kvp => kvp.Value.Count > SuspicionThreshold)
                .OrderByDescending(kvp => kvp.Value.Count)
                .Select(kvp => new SuspiciousPattern
                {
                    ExecutionCount = (int)Math.Min(int.MaxValue, kvp.Value.Count),
                    TotalTime = kvp.Value.TotalTimeMs,
                    AverageTime = kvp.Value.Count > 0 ? (double)kvp.Value.TotalTimeMs / kvp.Value.Count : 0,
                    StackTrace = kvp.Value.StackPreview
                })
                .ToList();

            return new N1DetectionReport
            {
                HasSuspiciousPatterns = suspiciousPatterns.Count > 0,
                Patterns = suspiciousPatterns,
                TotalPatterns = _queriesByHash.Count
            };
        }
    }

    /// <summary>Resets all tracked patterns.</summary>
    public void Reset()
    {
        lock (_lockObj) _queriesByHash.Clear();
    }

    private static string ComputeHash(string input)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(input), hash);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// N+1 detection report.
/// </summary>
public class N1DetectionReport
{
    /// <summary>Whether suspicious patterns were detected.</summary>
    public bool HasSuspiciousPatterns { get; set; }
    /// <summary>List of suspicious patterns found.</summary>
    public List<SuspiciousPattern> Patterns { get; set; } = new();
    /// <summary>Total number of patterns detected.</summary>
    public int TotalPatterns { get; set; }
}

/// <summary>
/// Suspicious query pattern.
/// </summary>
public class SuspiciousPattern
{
    /// <summary>Number of times this pattern executed.</summary>
    public int ExecutionCount { get; set; }
    /// <summary>Total execution time in milliseconds.</summary>
    public long TotalTime { get; set; }
    /// <summary>Average execution time in milliseconds.</summary>
    public double AverageTime { get; set; }
    /// <summary>Stack trace where the pattern was detected.</summary>
    public string StackTrace { get; set; } = string.Empty;
}

/// <summary>
/// Health check provider for dependency health.
/// </summary>
public interface IHealthCheckProvider
{
    /// <summary>Checks database health.</summary>
    Task<HealthStatus> CheckDatabaseAsync();
    /// <summary>Checks cache health.</summary>
    Task<HealthStatus> CheckCacheAsync();
    /// <summary>Checks audit system health.</summary>
    Task<HealthStatus> CheckAuditAsync();
    /// <summary>Checks security system health.</summary>
    Task<HealthStatus> CheckSecurityAsync();
}

/// <summary>
/// Health status of a component.
/// </summary>
public class HealthStatus
{
    /// <summary>Whether the component is healthy.</summary>
    public bool IsHealthy { get; set; }
    /// <summary>Name of the component checked.</summary>
    public string Component { get; set; } = string.Empty;
    /// <summary>Response time in milliseconds.</summary>
    public long ResponseTimeMs { get; set; }
    /// <summary>Status message or error details.</summary>
    public string? Message { get; set; }
}
