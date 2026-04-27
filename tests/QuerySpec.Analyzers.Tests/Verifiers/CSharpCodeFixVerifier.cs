using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace QuerySpec.Analyzers.Tests.Verifiers;

/// <summary>
/// Thin wrapper over the Roslyn code-fix-testing harness pre-wired to net8 reference assemblies.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer that produces the diagnostic to fix.</typeparam>
/// <typeparam name="TCodeFix">The code-fix provider under test.</typeparam>
public static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Warning);

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new(descriptor);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => VerifyAnalyzerAsync(source, System.Array.Empty<string>(), expected);

    public static Task VerifyAnalyzerAsync(string source, string[] additionalSources, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source,
        };
        foreach (var extra in additionalSources)
        {
            test.TestState.Sources.Add(extra);
        }
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(CancellationToken.None);
    }

    public static Task VerifyCodeFixAsync(string source, string fixedSource)
        => VerifyCodeFixAsync(source, System.Array.Empty<DiagnosticResult>(), fixedSource, System.Array.Empty<string>());

    public static Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        => VerifyCodeFixAsync(source, new[] { expected }, fixedSource, System.Array.Empty<string>());

    public static Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
        => VerifyCodeFixAsync(source, expected, fixedSource, System.Array.Empty<string>());

    public static Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource, string[] additionalSources)
    {
        var test = new Test
        {
            TestCode = source,
            FixedCode = fixedSource,
        };
        foreach (var extra in additionalSources)
        {
            test.TestState.Sources.Add(extra);
            test.FixedState.Sources.Add(extra);
        }
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(CancellationToken.None);
    }

    public sealed class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = QuerySpecReferenceAssemblies.Default;
        }
    }
}
