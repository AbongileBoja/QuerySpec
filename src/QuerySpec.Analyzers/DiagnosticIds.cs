namespace QuerySpec.Analyzers;

/// <summary>
/// Stable diagnostic identifiers emitted by the QuerySpec analyzer package. The IDs are part of
/// the package contract: changing one is a breaking change because it invalidates every
/// <c>#pragma warning disable</c>, <c>NoWarn</c>, and <c>EditorConfig</c> entry in consumer code.
/// </summary>
public static class DiagnosticIds
{
    /// <summary>
    /// QSPEC0001: a member access reads <c>GeoLocation.Latitude</c> or
    /// <c>GeoLocation.Longitude</c>. Migrate to <c>GeoCoordinate</c>.
    /// </summary>
    public const string GeoLocationMemberAccess = "QSPEC0001";

    /// <summary>
    /// QSPEC0002: a type reference, object creation, or member access targets
    /// <c>AdvancedFilterExpression</c>. Migrate to <c>FilterSpec</c>.
    /// </summary>
    public const string AdvancedFilterExpressionUsage = "QSPEC0002";

    /// <summary>
    /// QSPEC0003: an invocation calls <c>ICacheProvider.GetAsync&lt;T&gt;</c> or
    /// <c>ICacheProvider.SetAsync&lt;T&gt;</c>. Migrate to
    /// <c>ICacheStore.TryGetAsync&lt;T&gt;</c> / <c>SetValueAsync&lt;T&gt;</c>.
    /// </summary>
    public const string CacheProviderInvocation = "QSPEC0003";
}
