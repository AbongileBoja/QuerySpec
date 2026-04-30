using System.Diagnostics.Metrics;
using System.Reflection;

namespace QuerySpec.Core.Diagnostics;

/// <summary>
/// Meter and instruments published by QuerySpec for observability.
/// Subscribe via <c>dotnet-counters monitor --counters QuerySpec</c> or
/// <c>meterProviderBuilder.AddMeter("QuerySpec")</c> in OpenTelemetry.
/// </summary>
public static class QuerySpecMetrics
{
    /// <summary>The stable meter name. Use this constant when subscribing via <see cref="MeterListener"/> or OpenTelemetry.</summary>
    public const string MeterName = "QuerySpec";

    internal static readonly Meter Meter = new(
        MeterName,
        typeof(QuerySpecMetrics).Assembly.GetName().Version?.ToString() ?? "0.0.0");

    /// <summary>
    /// Total count of <c>ApplyFilter</c> / <c>ApplyFilterCached</c> invocations.
    /// Tags: <c>cached</c> (<c>true</c>|<c>false</c>), <c>entity_type</c> (short type name).
    /// </summary>
    internal static readonly Counter<long> FilterApplications = Meter.CreateCounter<long>(
        "queryspec.filter.applications",
        description: "Total count of FilterSpec.ApplyFilter invocations.");

    /// <summary>
    /// Total count of predicate-cache hits across all entity-type partitions.
    /// Tags: <c>cache_name</c>.
    /// </summary>
    internal static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "queryspec.cache.hits",
        description: "Total count of cache hits across all caches.");

    /// <summary>
    /// Total count of predicate-cache misses across all entity-type partitions.
    /// Tags: <c>cache_name</c>.
    /// </summary>
    internal static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "queryspec.cache.misses",
        description: "Total count of cache misses across all caches.");

    /// <summary>
    /// Time in milliseconds spent building (validating + compiling) a <c>FilterSpec</c> predicate on cache miss.
    /// </summary>
    internal static readonly Histogram<double> FilterBuildDuration = Meter.CreateHistogram<double>(
        "queryspec.filter.build.duration",
        unit: "ms",
        description: "Time spent building / compiling FilterSpec predicates.");

    /// <summary>
    /// Time in milliseconds spent evaluating an RLS predicate or filter.
    /// Tags: <c>resource_type</c>.
    /// </summary>
    internal static readonly Histogram<double> RlsEvaluationDuration = Meter.CreateHistogram<double>(
        "queryspec.rls.evaluation.duration",
        unit: "ms",
        description: "Time spent evaluating RLS predicates.");
}
