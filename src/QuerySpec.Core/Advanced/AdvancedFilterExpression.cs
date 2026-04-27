using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Advanced filter expression with temporal, geospatial, and custom operator support.
/// </summary>
/// <remarks>
/// Replaced by <see cref="FilterSpec"/>: an immutable <see langword="record"/> with
/// <see langword="init"/>-only accessors and value equality, safely shareable across threads
/// and cache layers without defensive copies. Round-trip via
/// <see cref="FilterSpec.FromMutable(AdvancedFilterExpression)"/> /
/// <see cref="FilterSpec.ToMutable"/>. The mutable POCO is obsolete (diagnostic id
/// <c>QSPEC0002</c>) and will be removed in 4.0; it remains for binary compatibility through the
/// 3.x line. Tracked in <see href="https://github.com/AbongileBoja/QuerySpec/issues/84">#84</see>.
/// </remarks>
[Obsolete("Use FilterSpec (immutable record). The mutable POCO will be removed in 4.0. See QSPEC0002.",
    error: false,
    DiagnosticId = "QSPEC0002",
    UrlFormat = "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/{0}.md")]
public class AdvancedFilterExpression
{
    private static readonly Regex FieldNamePattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Field name to filter on.</summary>
    public string Field { get; set; } = string.Empty;
    /// <summary>Filter operator to apply.</summary>
    public FilterOperator Operator { get; set; }
    /// <summary>Primary filter value.</summary>
    public object? Value { get; set; }
    /// <summary>Secondary filter value (for ranges, etc.).</summary>
    public object? ValueTo { get; set; }

    /// <summary>Whether string comparison is case-sensitive.</summary>
    public bool CaseSensitive { get; set; } = false;
    /// <summary>Whether to use regex for string matching.</summary>
    public bool UseRegex { get; set; } = false;
    /// <summary>Logical operator for combining nested filters.</summary>
    public LogicalOperator Logic { get; set; } = LogicalOperator.And;
    /// <summary>Nested filter expressions.</summary>
    public List<AdvancedFilterExpression>? Filters { get; set; }

    /// <summary>Start of temporal range.</summary>
    public DateTime? TemporalStart { get; set; }
    /// <summary>End of temporal range.</summary>
    public DateTime? TemporalEnd { get; set; }
    /// <summary>Whether to include deleted records in temporal queries.</summary>
    public bool IncludeDeletedRecords { get; set; } = false;

    /// <summary>Geographic location for geo queries.</summary>
    public GeoLocation? GeoLocation { get; set; }
    /// <summary>Geographic radius in kilometers.</summary>
    public decimal? GeoRadius { get; set; }

    /// <summary>Name of custom operator to use.</summary>
    public string? CustomOperatorName { get; set; }

    /// <summary>
    /// Previously a no-op flag intended to opt the result into masking. The flag was never wired
    /// into any translator or pipeline, so setting it produced no protective behaviour. Masking
    /// is configured globally via <c>DataMaskingEngine</c> and applied at projection time;
    /// setting it on a per-filter basis is not supported and the property will be removed in 3.0.
    /// </summary>
    /// <remarks>
    /// Setting this to <c>true</c> now causes <see cref="Validate"/> to return an error so a
    /// caller relying on a non-existent guarantee fails loudly instead of leaking unmasked data.
    /// </remarks>
    [Obsolete("MaskResult was a no-op in 1.x and 2.0. Configure masking via DataMaskingEngine and apply it at projection time. Setting this to true now fails Validate(). Property will be removed in 3.0.", error: false)]
    public bool MaskResult { get; set; } = false;

    /// <summary>
    /// Previously a no-op flag intended to opt the value into encryption. The flag was never
    /// wired into any translator or pipeline, so setting it produced no protective behaviour.
    /// Encryption is configured via <c>IEncryptionProvider</c> (e.g. <c>AesGcmEncryptionProvider</c>)
    /// and applied at storage time; setting it on a per-filter basis is not supported and the
    /// property will be removed in 3.0.
    /// </summary>
    /// <remarks>
    /// Setting this to <c>true</c> now causes <see cref="Validate"/> to return an error so a
    /// caller relying on a non-existent guarantee fails loudly instead of comparing plaintext.
    /// </remarks>
    [Obsolete("EncryptValue was a no-op in 1.x and 2.0. Configure encryption via IEncryptionProvider (AesGcmEncryptionProvider) and apply it at storage time. Setting this to true now fails Validate(). Property will be removed in 3.0.", error: false)]
    public bool EncryptValue { get; set; } = false;

    /// <summary>Initializes a new advanced filter expression.</summary>
    public AdvancedFilterExpression() { }

    /// <summary>
    /// Validates filter expression.
    /// </summary>
    /// <returns>A collection of error messages; empty when the filter is valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Field))
        {
            errors.Add("Field is required");
        }
        else if (!FieldNamePattern.IsMatch(Field))
        {
            errors.Add($"Invalid field name: {Field}");
        }

        if (TemporalStart.HasValue && TemporalEnd.HasValue && TemporalStart > TemporalEnd)
            errors.Add("TemporalStart must be before TemporalEnd");

        if (GeoLocation != null && GeoRadius.HasValue && GeoRadius <= 0)
            errors.Add("GeoRadius must be positive");

#pragma warning disable CS0618 // referencing obsolete members on the declaring type to enforce the no-op contract
        if (MaskResult)
            errors.Add("MaskResult is not a supported per-filter option (was a silent no-op in 1.x and 2.0). Configure masking via DataMaskingEngine at projection time.");
        if (EncryptValue)
            errors.Add("EncryptValue is not a supported per-filter option (was a silent no-op in 1.x and 2.0). Configure encryption via IEncryptionProvider at storage time.");
#pragma warning restore CS0618

        if (Filters?.Count > 0)
        {
            foreach (var filter in Filters)
            {
                errors.AddRange(filter.Validate());
            }
        }

        return errors;
    }

    /// <summary>
    /// Computes a stable 64-bit hash over the entire filter tree — Field, Operator, Logic,
    /// flags, values, temporal/geo coordinates, and all nested children. Two filters that
    /// differ in any structurally-meaningful way produce different hashes; two filters that
    /// are value-equal produce the same hash. Hash is derived from FNV-1a over culture-
    /// invariant string projections so results are deterministic across processes and runs
    /// (unlike <see cref="object.GetHashCode"/>, which can be randomized per AppDomain).
    /// </summary>
    /// <remarks>
    /// Intended as the cache key for a compiled-expression cache. Not a cryptographic hash;
    /// collisions are statistically possible but acceptable for this purpose — a collision
    /// would reuse a semantically-different expression, which callers can bound by pairing
    /// the hash with <see cref="Type"/> in the cache key.
    /// </remarks>
    /// <returns>A deterministic 64-bit hash over the entire filter tree.</returns>
    public long ComputeStableHash()
    {
        const long FnvOffset = unchecked((long)0xCBF29CE484222325UL);
        const long FnvPrime = 0x100000001B3L;

        long hash = FnvOffset;
        HashInto(ref hash, FnvPrime);
        return hash;
    }

    private void HashInto(ref long hash, long prime)
    {
        HashString(ref hash, prime, Field);
        HashLong(ref hash, prime, (long)Operator);
        HashLong(ref hash, prime, (long)Logic);
        HashLong(ref hash, prime, CaseSensitive ? 1 : 0);
        HashLong(ref hash, prime, UseRegex ? 1 : 0);
        HashLong(ref hash, prime, IncludeDeletedRecords ? 1 : 0);
#pragma warning disable CS0618 // hashing obsolete members preserves cache-key shape; Validate() rejects non-default values
        HashLong(ref hash, prime, MaskResult ? 1 : 0);
        HashLong(ref hash, prime, EncryptValue ? 1 : 0);
#pragma warning restore CS0618

        HashString(ref hash, prime, FormatValue(Value));
        HashString(ref hash, prime, FormatValue(ValueTo));
        HashString(ref hash, prime, CustomOperatorName ?? " ");

        HashLong(ref hash, prime, TemporalStart?.Ticks ?? -1);
        HashLong(ref hash, prime, TemporalEnd?.Ticks ?? -1);

        if (GeoLocation != null)
        {
            HashString(ref hash, prime, GeoLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
            HashString(ref hash, prime, GeoLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (GeoRadius.HasValue)
        {
            HashString(ref hash, prime, GeoRadius.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (Filters != null)
        {
            HashLong(ref hash, prime, Filters.Count);
            foreach (var child in Filters)
            {
                child.HashInto(ref hash, prime);
            }
        }
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return " ";
        if (value is string s) return "s:" + s;
        if (value is System.Collections.IEnumerable e and not string)
        {
            var sb = new System.Text.StringBuilder("[");
            foreach (var item in e)
            {
                sb.Append(FormatValue(item)).Append('|');
            }
            sb.Append(']');
            return sb.ToString();
        }
        if (value is IFormattable f)
            return value.GetType().Name + ":" + f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        return value.GetType().Name + ":" + value.ToString();
    }

    private static void HashString(ref long hash, long prime, string? s)
    {
        if (s is null) { hash = unchecked(hash * prime); return; }
        for (var i = 0; i < s.Length; i++)
        {
            hash = unchecked((hash ^ s[i]) * prime);
        }
        hash = unchecked((hash ^ 0xFF) * prime); // terminator
    }

    private static void HashLong(ref long hash, long prime, long v)
    {
        hash = unchecked((hash ^ v) * prime);
    }
}

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
