using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace QuerySpec.Analyzers.Tests.Verifiers;

/// <summary>
/// Thin wrapper over the Roslyn analyzer-testing harness pre-wired to net8 reference assemblies.
/// Tests inline their own stub types for the deprecated members so the analyzer is exercised in
/// isolation from the production [Obsolete(DiagnosticId="QSPEC####")] attribute path.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new(diagnosticId, DiagnosticSeverity.Warning);

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new(descriptor);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => VerifyAnalyzerAsync(source, additionalSources: System.Array.Empty<string>(), expected);

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

    public sealed class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = QuerySpecReferenceAssemblies.Default;
        }
    }
}
