using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyAnalyzer = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.AdvancedFilterExpressionAnalyzer>;
using VerifyCodeFix = QuerySpec.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<QuerySpec.Analyzers.AdvancedFilterExpressionAnalyzer, QuerySpec.Analyzers.AdvancedFilterExpressionCodeFixProvider>;

namespace QuerySpec.Analyzers.Tests;

public sealed class AdvancedFilterExpressionAnalyzerTests
{
    private static readonly string[] StubSources = { TestStubs.FilterTypes };

    [Fact]
    public async Task ObjectCreation_With_Initializer_Triggers_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new {|#0:AdvancedFilterExpression|}
                {
                    Field = "Status",
                    Operator = FilterOperator.Equal,
                    Value = "Active",
                };
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task ObjectCreation_With_Default_Constructor_Triggers_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new {|#0:AdvancedFilterExpression|}();
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task ObjectCreation_Of_FilterSpec_Does_Not_Trigger()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new FilterSpec
                {
                    Field = "Status",
                    Operator = FilterOperator.Equal,
                    Value = "Active",
                };
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task Suppression_Pragma_Silences_Diagnostic()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M()
                {
            #pragma warning disable QSPEC0002
                    return new AdvancedFilterExpression { Field = "X" };
            #pragma warning restore QSPEC0002
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task Analyzer_Suppresses_When_Type_Already_Has_Matching_Obsolete_DiagnosticId()
    {
        const string ObsoleteStub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                [Obsolete("Use FilterSpec", DiagnosticId = "QSPEC0002")]
                public class AdvancedFilterExpression
                {
                    public string Field { get; set; } = "";
                }
            }
            """;
        const string Source = """
            class C
            {
            #pragma warning disable QSPEC0002
                object M() => new QuerySpec.Core.Advanced.AdvancedFilterExpression();
            #pragma warning restore QSPEC0002
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { ObsoleteStub });
    }

    [Fact]
    public void HelpLinkUri_Points_At_GitHub_Diagnostic_Reference()
    {
        var descriptor = DiagnosticDescriptors.AdvancedFilterExpressionUsage;
        Assert.Equal(
            "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/QSPEC0002.md",
            descriptor.HelpLinkUri);
        Assert.Equal("QSPEC0002", descriptor.Id);
    }

    [Fact]
    public async Task CodeFix_Rewrites_AdvancedFilterExpression_To_FilterSpec()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new {|#0:AdvancedFilterExpression|}
                {
                    Field = "Status",
                    Operator = FilterOperator.Equal,
                    Value = "Active",
                };
            }
            """;

        const string Fixed = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new FilterSpec
                {
                    Field = "Status",
                    Operator = FilterOperator.Equal,
                    Value = "Active",
                };
            }
            """;

        var expected = VerifyCodeFix.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyCodeFix.VerifyCodeFixAsync(Source, new[] { expected }, Fixed, StubSources);
    }

    [Fact]
    public async Task FixAll_Rewrites_Multiple_Object_Creations_In_Document()
    {
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object A() => new {|#0:AdvancedFilterExpression|} { Field = "A" };
                object B() => new {|#1:AdvancedFilterExpression|} { Field = "B" };
                object D() => new {|#2:AdvancedFilterExpression|}();
            }
            """;

        const string Fixed = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object A() => new FilterSpec { Field = "A" };
                object B() => new FilterSpec { Field = "B" };
                object D() => new FilterSpec();
            }
            """;

        var diagnostics = new[]
        {
            VerifyCodeFix.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage).WithLocation(0),
            VerifyCodeFix.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage).WithLocation(1),
            VerifyCodeFix.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage).WithLocation(2),
        };

        await VerifyCodeFix.VerifyCodeFixAsync(Source, diagnostics, Fixed, StubSources);
    }
}
