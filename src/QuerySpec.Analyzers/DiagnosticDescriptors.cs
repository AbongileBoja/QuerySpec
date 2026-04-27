using Microsoft.CodeAnalysis;

namespace QuerySpec.Analyzers;

/// <summary>
/// Shared <see cref="DiagnosticDescriptor"/> instances for every QuerySpec migration diagnostic.
/// Descriptors are <see langword="static"/> <see langword="readonly"/> singletons so the analyzer
/// host can fingerprint them across compilations; do not allocate new descriptors per
/// <c>ReportDiagnostic</c> call.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "QuerySpec.Migration";

    private const string HelpLinkBase =
        "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/";

    /// <summary>
    /// QSPEC0001 — <c>GeoLocation.Latitude</c> / <c>GeoLocation.Longitude</c> member access.
    /// </summary>
    public static readonly DiagnosticDescriptor GeoLocationMemberAccess = new(
        id: DiagnosticIds.GeoLocationMemberAccess,
        title: "Use GeoCoordinate instead of GeoLocation.Latitude/.Longitude",
        messageFormat: "'GeoLocation.{0}' is deprecated; use 'GeoCoordinate' (call 'ToGeoCoordinate()' to migrate)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "GeoLocation exposes decimal latitude/longitude that contradicts every spatial library on .NET. " +
            "GeoCoordinate is an immutable readonly record struct with double components, validated " +
            "construction, and ISO 6709 round-trip. Will be removed in QuerySpec 4.0.",
        helpLinkUri: HelpLinkBase + DiagnosticIds.GeoLocationMemberAccess + ".md");

    /// <summary>
    /// QSPEC0002 — usage of the mutable POCO <c>AdvancedFilterExpression</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor AdvancedFilterExpressionUsage = new(
        id: DiagnosticIds.AdvancedFilterExpressionUsage,
        title: "Use FilterSpec instead of AdvancedFilterExpression",
        messageFormat: "'AdvancedFilterExpression' is deprecated; use 'FilterSpec' (immutable record with init accessors)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "AdvancedFilterExpression is a mutable POCO that leaks aliasing into compiled-expression " +
            "caches and forces defensive copies for safe sharing. FilterSpec is a sealed record with " +
            "init-only accessors, structural value equality, and the same Validate/ComputeStableHash " +
            "semantics. Will be removed in QuerySpec 4.0.",
        helpLinkUri: HelpLinkBase + DiagnosticIds.AdvancedFilterExpressionUsage + ".md");

    /// <summary>
    /// QSPEC0003 — invocation of <c>ICacheProvider.GetAsync</c> / <c>SetAsync</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor CacheProviderInvocation = new(
        id: DiagnosticIds.CacheProviderInvocation,
        title: "Use ICacheStore instead of ICacheProvider GetAsync/SetAsync",
        messageFormat: "'ICacheProvider.{0}' is deprecated; use 'ICacheStore.{1}' (no class constraint, value-type-friendly)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "ICacheProvider.GetAsync<T>/SetAsync<T> require 'where T : class', excluding value " +
            "types and producing ambiguous null-on-miss reads. ICacheStore.TryGetAsync<T> returns " +
            "CacheResult<T> whose HasValue flag distinguishes a hit on default(T) from a miss. " +
            "Will be removed in QuerySpec 4.0.",
        helpLinkUri: HelpLinkBase + DiagnosticIds.CacheProviderInvocation + ".md");
}
