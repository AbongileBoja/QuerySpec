using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// Maximum number of managed stack frames sampled per <see cref="RecordQuery"/> call. The
    /// preview and hash key derive from these frames; frames beyond this depth are not consulted.
    /// Bounded so the per-call cost is constant regardless of call depth.
    /// </summary>
    public int MaxStackFrames { get; set; } = 16;

    private sealed class QueryInfo
    {
        public string StackPreview = string.Empty;
        public long Count;
        public long TotalTimeMs;
        public long MaxTimeMs;
    }

    private readonly Dictionary<string, QueryInfo> _queriesByHash = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _lockObj = new();
#else
    private readonly object _lockObj = new();
#endif

    /// <summary>Initializes a new N+1 detection engine.</summary>
    public N1DetectionEngine() { }

    /// <summary>
    /// Records a query execution. Safe to call from multiple threads.
    /// </summary>
    /// <remarks>
    /// Captures up to <see cref="MaxStackFrames"/> managed frames via <see cref="StackTrace"/> with
    /// <c>fNeedFileInfo: false</c>; full <see cref="Environment.StackTrace"/> (which formats every
    /// frame and resolves PDB info) is not used because its multi-microsecond per-call cost can
    /// exceed the cost of fast queries it's meant to track.
    /// </remarks>
    /// <param name="sql">SQL text of the query. Retained only as part of the call-stack key when relevant; not stored verbatim.</param>
    /// <param name="executionTimeMs">Execution time in milliseconds, accumulated into the per-pattern aggregates.</param>
    [RequiresUnreferencedCode("RecordQuery walks the managed stack via System.Diagnostics.StackFrame.GetMethod() to build a call-site fingerprint. Under trimming, method metadata may be incomplete and patterns will collapse to '?' segments, reducing N+1 detection precision. Consider disabling N+1 detection in trimmed deployments or use a structured logging approach upstream of QuerySpec.")]
    public void RecordQuery(string sql, long executionTimeMs)
    {
        var stack = BuildStackPreview();
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
    /// <returns>A snapshot report listing call-stack patterns whose execution count exceeds <see cref="SuspicionThreshold"/>.</returns>
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

    [RequiresUnreferencedCode("BuildStackPreview walks the managed stack via StackFrame.GetMethod(); under trimming the resolved method metadata may be incomplete.")]
    private string BuildStackPreview()
    {
        // skipFrames: 1 elides BuildStackPreview; the immediate caller (RecordQuery) is included
        // because it's a stable anchor that helps disambiguate callers who instrument the engine
        // through their own helper.
        var st = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
        var frameCount = Math.Min(st.FrameCount, MaxStackFrames);
        if (frameCount == 0) return string.Empty;

        var sb = new StringBuilder(MaxStackFrames * 64);
        for (var i = 0; i < frameCount; i++)
        {
            var method = st.GetFrame(i)?.GetMethod();
            if (method is null) continue;
            sb.Append(method.DeclaringType?.FullName ?? "?");
            sb.Append('.');
            sb.Append(method.Name);
            sb.Append('\n');
        }
        return sb.ToString();
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
/// <remarks>No concrete implementation ships in this version. Tracked for evaluation in 3.0.</remarks>
public interface IHealthCheckProvider
{
    /// <summary>Checks database health.</summary>
    /// <returns>The database component's <see cref="HealthStatus"/>.</returns>
    Task<HealthStatus> CheckDatabaseAsync();
    /// <summary>Checks cache health.</summary>
    /// <returns>The cache component's <see cref="HealthStatus"/>.</returns>
    Task<HealthStatus> CheckCacheAsync();
    /// <summary>Checks audit system health.</summary>
    /// <returns>The audit component's <see cref="HealthStatus"/>.</returns>
    Task<HealthStatus> CheckAuditAsync();
    /// <summary>Checks security system health.</summary>
    /// <returns>The security component's <see cref="HealthStatus"/>.</returns>
    Task<HealthStatus> CheckSecurityAsync();
}

/// <summary>
/// Health status of a component.
/// </summary>
/// <remarks>No concrete implementation ships in this version. Tracked for evaluation in 3.0.</remarks>
[ExcludeFromCodeCoverage]
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
