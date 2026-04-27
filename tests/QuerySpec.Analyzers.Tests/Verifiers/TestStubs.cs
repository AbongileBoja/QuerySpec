namespace QuerySpec.Analyzers.Tests.Verifiers;

/// <summary>
/// Shared stub source fragments for analyzer tests. The stubs reproduce the public shape of the
/// QuerySpec.Core types under test (names, namespaces, signatures) without the production
/// <c>[Obsolete(DiagnosticId = "QSPEC####")]</c> attributes — tests therefore observe ONLY the
/// analyzer's emitted diagnostic, not the compiler's already-shipped obsolete warning. This is
/// the same isolation strategy Roslyn's own analyzer test suite uses for OptionalAttribute and
/// SuppressMessageAttribute scenarios.
/// </summary>
internal static class TestStubs
{
    /// <summary>Stub of QuerySpec.Core.Advanced.GeoLocation and GeoCoordinate plus the migration helper.</summary>
    public const string GeoTypes = """
        namespace QuerySpec.Core.Advanced
        {
            public class GeoLocation
            {
                public decimal Latitude { get; set; }
                public decimal Longitude { get; set; }
                public GeoLocation() { }
                public GeoLocation(decimal latitude, decimal longitude) { Latitude = latitude; Longitude = longitude; }
                public GeoCoordinate ToGeoCoordinate() => new((double)Latitude, (double)Longitude);
            }

            public readonly record struct GeoCoordinate
            {
                public double Latitude { get; }
                public double Longitude { get; }
                public GeoCoordinate(double latitude, double longitude) { Latitude = latitude; Longitude = longitude; }
            }
        }
        """;

    /// <summary>Stub of QuerySpec.Core.Advanced.AdvancedFilterExpression and FilterSpec.</summary>
    public const string FilterTypes = """
        namespace QuerySpec.Core.Advanced
        {
            public enum FilterOperator { Equal, NotEqual }

            public class AdvancedFilterExpression
            {
                public string Field { get; set; } = "";
                public FilterOperator Operator { get; set; }
                public object? Value { get; set; }
                public AdvancedFilterExpression() { }
            }

            public sealed record FilterSpec
            {
                public string Field { get; init; } = "";
                public FilterOperator Operator { get; init; }
                public object? Value { get; init; }
            }
        }
        """;

    /// <summary>Stub of QuerySpec.Core.Caching.ICacheProvider, ICacheStore, CacheResult, and a dual-interface concrete provider.</summary>
    public const string CacheTypes = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace QuerySpec.Core.Caching
        {
            public readonly struct CacheResult<T>
            {
                public bool HasValue { get; }
                public T Value { get; }
                public CacheResult(bool hasValue, T value) { HasValue = hasValue; Value = value; }
                public T? GetValueOrDefault() => HasValue ? Value : default;
                public T GetValueOrDefault(T fallback) => HasValue ? Value : fallback;
            }

            public interface ICacheProvider
            {
                ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
                ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
                ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
            }

            public interface ICacheStore
            {
                ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);
                ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
                ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
            }

            // Mirrors the shipping MemoryCacheProvider/DistributedCacheProvider/MultiLevelCache
            // shape: dual-interface so a consumer who already has the concrete instance can call
            // either contract. The QSPEC0003 codefix's well-typed scenario.
            public class TestCacheProvider : ICacheProvider, ICacheStore
            {
                public ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class => default;
                public ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class => default;
                public ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default) => default;
                public ValueTask SetValueAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) => default;
                public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => default;
            }
        }
        """;
}
