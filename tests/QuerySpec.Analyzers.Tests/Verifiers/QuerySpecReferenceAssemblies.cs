using Microsoft.CodeAnalysis.Testing;

namespace QuerySpec.Analyzers.Tests.Verifiers;

/// <summary>
/// Reference-assembly bundle every analyzer test inherits. Pinned to .NET 8 so the verifier
/// resolves a stable BCL surface across CI runners; the runtime test project itself executes on
/// net8/9/10 unchanged.
/// </summary>
internal static class QuerySpecReferenceAssemblies
{
    public static ReferenceAssemblies Default { get; } = ReferenceAssemblies.Net.Net80;
}
