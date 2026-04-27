using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Immutable, value-equal description of an advanced filter — the 4.0 successor to
/// <see cref="AdvancedFilterExpression"/>. Modeled as a <see langword="record"/> with
/// <see langword="init"/>-only accessors so filter graphs are safely shareable across threads
/// and cache layers without defensive copies.
/// </summary>
/// <remarks>
/// <para>
/// Round-trips losslessly with <see cref="AdvancedFilterExpression"/> via
/// <see cref="FromMutable(AdvancedFilterExpression)"/> and <see cref="ToMutable"/>; the EF Core
/// translator's <c>FilterSpec</c> overload uses <c>ToMutable</c> internally so the deprecation
/// window can stage adoption with minimal churn in the translator codebase.
/// </para>
/// <para>
/// Children are exposed as <see cref="IReadOnlyList{T}"/> rather than <c>ImmutableArray</c> to
/// avoid a new dependency on <c>System.Collections.Immutable</c>. Equality and stable-hash both
/// recurse into children explicitly so structural equality holds across <c>List</c> vs <c>T[]</c>
/// concrete representations of the same logical tree.
/// </para>
/// </remarks>
public sealed record FilterSpec
{
    private static readonly Regex FieldNamePattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Field name to filter on. Defaults to the empty string.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Filter operator to apply.</summary>
    public FilterOperator Operator { get; init; }

    /// <summary>Primary filter value.</summary>
    public object? Value { get; init; }

    /// <summary>Secondary filter value (for ranges, etc.).</summary>
    public object? ValueTo { get; init; }

    /// <summary>Whether string comparison is case-sensitive.</summary>
    public bool CaseSensitive { get; init; }

    /// <summary>Whether to use regex for string matching.</summary>
    public bool UseRegex { get; init; }

    /// <summary>Logical operator for combining nested filters.</summary>
    public LogicalOperator Logic { get; init; } = LogicalOperator.And;

    /// <summary>Nested filter expressions. Defaults to empty.</summary>
    public IReadOnlyList<FilterSpec> Filters { get; init; } = Array.Empty<FilterSpec>();

    /// <summary>Start of temporal range.</summary>
    public DateTime? TemporalStart { get; init; }

    /// <summary>End of temporal range.</summary>
    public DateTime? TemporalEnd { get; init; }

    /// <summary>Whether to include deleted records in temporal queries.</summary>
    public bool IncludeDeletedRecords { get; init; }

    /// <summary>Geographic location for geo queries (legacy decimal-based shape, retained for round-trip parity).</summary>
    public GeoLocation? GeoLocation { get; init; }

    /// <summary>Geographic radius in kilometres.</summary>
    public decimal? GeoRadius { get; init; }

    /// <summary>Name of custom operator to use.</summary>
    public string? CustomOperatorName { get; init; }

    /// <summary>
    /// Validates the filter tree. Mirrors the legacy <see cref="AdvancedFilterExpression.Validate"/>
    /// semantics; recurses into <see cref="Filters"/>.
    /// </summary>
    /// <returns>An enumeration of error messages; empty when the filter is valid.</returns>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();
        ValidateInto(errors);
        return errors;
    }

    private void ValidateInto(List<string> errors)
    {
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

        foreach (var child in Filters)
            child.ValidateInto(errors);
    }

    /// <summary>
    /// Computes a deterministic 64-bit hash over the entire filter tree. Two structurally-equal
    /// filters produce the same hash; semantically-distinct filters produce different hashes.
    /// Hash semantics match <see cref="AdvancedFilterExpression.ComputeStableHash"/> so cached
    /// keys round-trip across the deprecation window.
    /// </summary>
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

        HashString(ref hash, prime, FormatValue(Value));
        HashString(ref hash, prime, FormatValue(ValueTo));
        HashString(ref hash, prime, CustomOperatorName ?? " ");

        HashLong(ref hash, prime, TemporalStart?.Ticks ?? -1);
        HashLong(ref hash, prime, TemporalEnd?.Ticks ?? -1);

        if (GeoLocation != null)
        {
#pragma warning disable QSPEC0001 // round-trip parity: hash legacy decimal lat/long
            HashString(ref hash, prime, GeoLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
            HashString(ref hash, prime, GeoLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
#pragma warning restore QSPEC0001
        }
        if (GeoRadius.HasValue)
        {
            HashString(ref hash, prime, GeoRadius.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        HashLong(ref hash, prime, Filters.Count);
        foreach (var child in Filters)
            child.HashInto(ref hash, prime);
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return " ";
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
        return value.GetType().Name + ":" + value;
    }

    private static void HashString(ref long hash, long prime, string? s)
    {
        if (s is null) { hash = unchecked(hash * prime); return; }
        for (var i = 0; i < s.Length; i++)
        {
            hash = unchecked((hash ^ s[i]) * prime);
        }
        hash = unchecked((hash ^ 0xFF) * prime);
    }

    private static void HashLong(ref long hash, long prime, long v)
    {
        hash = unchecked((hash ^ v) * prime);
    }

    /// <summary>
    /// Projects a legacy <see cref="AdvancedFilterExpression"/> tree into an immutable
    /// <see cref="FilterSpec"/>. Field-by-field copy; obsolete <c>MaskResult</c> /
    /// <c>EncryptValue</c> flags are dropped (they were never honoured) and child trees recurse.
    /// </summary>
    /// <param name="legacy">The legacy expression to project. Must not be null.</param>
    /// <returns>An equivalent <see cref="FilterSpec"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="legacy"/> is null.</exception>
#pragma warning disable QSPEC0002 // round-trip helpers reference the obsolete type by design
    public static FilterSpec FromMutable(AdvancedFilterExpression legacy)
    {
        if (legacy is null) throw new ArgumentNullException(nameof(legacy));
        return new FilterSpec
        {
            Field = legacy.Field,
            Operator = legacy.Operator,
            Value = legacy.Value,
            ValueTo = legacy.ValueTo,
            CaseSensitive = legacy.CaseSensitive,
            UseRegex = legacy.UseRegex,
            Logic = legacy.Logic,
            Filters = legacy.Filters is { Count: > 0 } children
                ? children.Select(FromMutable).ToArray()
                : Array.Empty<FilterSpec>(),
            TemporalStart = legacy.TemporalStart,
            TemporalEnd = legacy.TemporalEnd,
            IncludeDeletedRecords = legacy.IncludeDeletedRecords,
            GeoLocation = legacy.GeoLocation,
            GeoRadius = legacy.GeoRadius,
            CustomOperatorName = legacy.CustomOperatorName,
        };
    }

    /// <summary>
    /// Projects this <see cref="FilterSpec"/> into a fresh mutable
    /// <see cref="AdvancedFilterExpression"/> tree. Used by the EF Core translator's
    /// <c>FilterSpec</c> overload to route through the existing predicate-builder code path.
    /// </summary>
    /// <returns>An equivalent <see cref="AdvancedFilterExpression"/>.</returns>
    public AdvancedFilterExpression ToMutable()
    {
        var legacy = new AdvancedFilterExpression
        {
            Field = Field,
            Operator = Operator,
            Value = Value,
            ValueTo = ValueTo,
            CaseSensitive = CaseSensitive,
            UseRegex = UseRegex,
            Logic = Logic,
            TemporalStart = TemporalStart,
            TemporalEnd = TemporalEnd,
            IncludeDeletedRecords = IncludeDeletedRecords,
            GeoLocation = GeoLocation,
            GeoRadius = GeoRadius,
            CustomOperatorName = CustomOperatorName,
        };
        if (Filters.Count > 0)
        {
            legacy.Filters = new List<AdvancedFilterExpression>(Filters.Count);
            foreach (var child in Filters)
                legacy.Filters.Add(child.ToMutable());
        }
        return legacy;
#pragma warning restore QSPEC0002
    }

    /// <summary>
    /// Structural value equality across the entire tree, including <see cref="Filters"/>.
    /// </summary>
    /// <param name="other">The other filter to compare. May be null.</param>
    /// <returns><see langword="true"/> when the two trees are structurally equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(FilterSpec? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (!string.Equals(Field, other.Field, StringComparison.Ordinal)) return false;
        if (Operator != other.Operator) return false;
        if (Logic != other.Logic) return false;
        if (CaseSensitive != other.CaseSensitive) return false;
        if (UseRegex != other.UseRegex) return false;
        if (IncludeDeletedRecords != other.IncludeDeletedRecords) return false;
        if (TemporalStart != other.TemporalStart) return false;
        if (TemporalEnd != other.TemporalEnd) return false;
        if (GeoRadius != other.GeoRadius) return false;
        if (!string.Equals(CustomOperatorName, other.CustomOperatorName, StringComparison.Ordinal)) return false;
        if (!Equals(Value, other.Value)) return false;
        if (!Equals(ValueTo, other.ValueTo)) return false;
        if (!GeoLocationEquals(GeoLocation, other.GeoLocation)) return false;

        if (Filters.Count != other.Filters.Count) return false;
        for (var i = 0; i < Filters.Count; i++)
        {
            if (!Equals(Filters[i], other.Filters[i])) return false;
        }
        return true;
    }

    /// <summary>Hash derived from <see cref="ComputeStableHash"/> for compatibility with the equality contract.</summary>
    /// <returns>A 32-bit hash mixing the upper and lower halves of the stable 64-bit hash.</returns>
    public override int GetHashCode()
    {
        var h64 = ComputeStableHash();
        return unchecked((int)h64) ^ (int)(h64 >> 32);
    }

    private static bool GeoLocationEquals(GeoLocation? a, GeoLocation? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
#pragma warning disable QSPEC0001 // round-trip parity: compare legacy decimal lat/long
        return a.Latitude == b.Latitude && a.Longitude == b.Longitude;
#pragma warning restore QSPEC0001
    }
}
