using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyAnalyzer = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.GeoLocationMemberAccessAnalyzer>;
using VerifyCodeFix = QuerySpec.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<QuerySpec.Analyzers.GeoLocationMemberAccessAnalyzer, QuerySpec.Analyzers.GeoLocationMemberAccessCodeFixProvider>;

namespace QuerySpec.Analyzers.Tests;

public sealed class GeoLocationMemberAccessAnalyzerTests
{
    private static readonly string[] StubSources = { TestStubs.GeoTypes };

    [Fact]
    public async Task Latitude_Read_On_GeoLocation_Triggers_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoLocation legacy)
                {
                    return (double)legacy.{|#0:Latitude|};
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Latitude");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task Longitude_Read_On_GeoLocation_Triggers_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoLocation legacy)
                {
                    return (double)legacy.{|#0:Longitude|};
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Longitude");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task Latitude_Read_On_GeoCoordinate_Does_Not_Trigger()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoCoordinate coord) => coord.Latitude;
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task Member_On_Unrelated_Type_Does_Not_Trigger()
    {
        const string Source = """
            class Other
            {
                public double Latitude { get; set; }
                public double Longitude { get; set; }
            }

            class C
            {
                double M(Other o) => o.Latitude + o.Longitude;
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source);
    }

    [Fact]
    public async Task Suppression_Pragma_Silences_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoLocation legacy)
                {
            #pragma warning disable QSPEC0001
                    var lat = (double)legacy.Latitude;
            #pragma warning restore QSPEC0001
                    return lat;
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task Analyzer_Suppresses_When_Property_Already_Has_Matching_Obsolete_DiagnosticId()
    {
        const string ObsoleteStub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                public class GeoLocation
                {
                    [Obsolete("Use GeoCoordinate", DiagnosticId = "QSPEC0001")]
                    public decimal Latitude { get; set; }
                }

                public readonly record struct GeoCoordinate
                {
                    public double Latitude { get; }
                }
            }
            """;
        const string Source = """
            class C
            {
                decimal M(QuerySpec.Core.Advanced.GeoLocation legacy)
                {
            #pragma warning disable QSPEC0001
                    return legacy.Latitude;
            #pragma warning restore QSPEC0001
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { ObsoleteStub });
    }

    [Fact]
    public void HelpLinkUri_Points_At_GitHub_Diagnostic_Reference()
    {
        var descriptor = DiagnosticDescriptors.GeoLocationMemberAccess;
        Assert.Equal(
            "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/QSPEC0001.md",
            descriptor.HelpLinkUri);
        Assert.Equal("QSPEC0001", descriptor.Id);
    }

    [Fact]
    public async Task CodeFix_Rewrites_Latitude_Read_As_ToGeoCoordinate_Call()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoLocation legacy)
                {
                    return (double)legacy.{|#0:Latitude|};
                }
            }
            """;

        const string Fixed = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double M(GeoLocation legacy)
                {
                    return (double)legacy.ToGeoCoordinate().Latitude;
                }
            }
            """;

        var expected = VerifyCodeFix.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Latitude");

        await VerifyCodeFix.VerifyCodeFixAsync(Source, new[] { expected }, Fixed, StubSources);
    }

    [Fact]
    public async Task FixAll_Rewrites_Multiple_Latitude_Longitude_Reads_In_Document()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double Sum(GeoLocation a, GeoLocation b)
                {
                    var x = (double)a.{|#0:Latitude|};
                    var y = (double)a.{|#1:Longitude|};
                    var z = (double)b.{|#2:Latitude|};
                    return x + y + z;
                }
            }
            """;

        const string Fixed = """
            using QuerySpec.Core.Advanced;

            class C
            {
                double Sum(GeoLocation a, GeoLocation b)
                {
                    var x = (double)a.ToGeoCoordinate().Latitude;
                    var y = (double)a.ToGeoCoordinate().Longitude;
                    var z = (double)b.ToGeoCoordinate().Latitude;
                    return x + y + z;
                }
            }
            """;

        var diagnostics = new[]
        {
            VerifyCodeFix.Diagnostic(DiagnosticIds.GeoLocationMemberAccess).WithLocation(0).WithArguments("Latitude"),
            VerifyCodeFix.Diagnostic(DiagnosticIds.GeoLocationMemberAccess).WithLocation(1).WithArguments("Longitude"),
            VerifyCodeFix.Diagnostic(DiagnosticIds.GeoLocationMemberAccess).WithLocation(2).WithArguments("Latitude"),
        };

        await VerifyCodeFix.VerifyCodeFixAsync(Source, diagnostics, Fixed, StubSources);
    }
}
