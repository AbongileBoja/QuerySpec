using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyAdvanced = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.AdvancedFilterExpressionAnalyzer>;
using VerifyGeo = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.GeoLocationMemberAccessAnalyzer>;

namespace QuerySpec.Analyzers.Tests;

/// <summary>
/// Covers the HasMatchingObsoleteAttribute branches in both
/// AdvancedFilterExpressionAnalyzer and GeoLocationMemberAccessAnalyzer:
///   - [Obsolete] with no DiagnosticId → does NOT suppress; diagnostic is emitted
///   - [Obsolete(DiagnosticId = "OTHER")] → different id → does NOT suppress; diagnostic is emitted
///   - No AdvancedFilterExpression type in compilation → early return (no diagnostic)
/// </summary>
public sealed class AnalyzerObsoleteAttributeEdgeCaseTests
{
    // ── AdvancedFilterExpressionAnalyzer: Obsolete with no DiagnosticId ───────

    [Fact]
    public async Task AdvancedFilter_ObsoleteWithoutDiagnosticId_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                [Obsolete("Use FilterSpec")]
                public class AdvancedFilterExpression
                {
                    public string Field { get; set; } = "";
                }

                public enum FilterOperator { Equal }

                public sealed record FilterSpec
                {
                    public string Field { get; init; } = "";
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
            #pragma warning disable CS0618
                object M() => new {|#0:AdvancedFilterExpression|}();
            #pragma warning restore CS0618
            }
            """;

        var expected = VerifyAdvanced.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyAdvanced.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    [Fact]
    public async Task AdvancedFilter_ObsoleteWithDifferentDiagnosticId_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                [Obsolete("Use FilterSpec", DiagnosticId = "OTHER001")]
                public class AdvancedFilterExpression
                {
                    public string Field { get; set; } = "";
                }

                public enum FilterOperator { Equal }

                public sealed record FilterSpec
                {
                    public string Field { get; init; } = "";
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
            #pragma warning disable OTHER001
                object M() => new {|#0:AdvancedFilterExpression|}();
            #pragma warning restore OTHER001
            }
            """;

        var expected = VerifyAdvanced.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyAdvanced.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── GeoLocationMemberAccessAnalyzer: Obsolete with no DiagnosticId ────────

    [Fact]
    public async Task Geo_ObsoleteWithoutDiagnosticId_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                public class GeoLocation
                {
                    [Obsolete("Use GeoCoordinate")]
                    public decimal Latitude { get; set; }
                    [Obsolete("Use GeoCoordinate")]
                    public decimal Longitude { get; set; }
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
            #pragma warning disable CS0618
                decimal M(GeoLocation g) => g.{|#0:Latitude|};
            #pragma warning restore CS0618
            }
            """;

        var expected = VerifyGeo.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Latitude");

        await VerifyGeo.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    [Fact]
    public async Task Geo_ObsoleteWithDifferentDiagnosticId_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                public class GeoLocation
                {
                    [Obsolete("Use GeoCoordinate", DiagnosticId = "OTHER001")]
                    public decimal Latitude { get; set; }
                    public decimal Longitude { get; set; }
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
            #pragma warning disable OTHER001
                decimal M(GeoLocation g) => g.{|#0:Latitude|};
            #pragma warning restore OTHER001
            }
            """;

        var expected = VerifyGeo.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Latitude");

        await VerifyGeo.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── GeoLocationMemberAccessAnalyzer: non-Latitude/Longitude member access ─

    [Fact]
    public async Task Geo_AccessToOtherMember_NoDiagnostic()
    {
        const string Stub = TestStubs.GeoTypes;
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                GeoCoordinate M(GeoLocation g) => g.ToGeoCoordinate();
            }
            """;

        await VerifyGeo.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── GeoLocationMemberAccessAnalyzer: no GeoLocation type → no diagnostic ─

    [Fact]
    public async Task Geo_NoGeoLocationTypeInCompilation_NoDiagnostic()
    {
        const string Source = """
            class C
            {
                int M() => 1;
            }
            """;

        await VerifyGeo.VerifyAnalyzerAsync(Source);
    }
}
