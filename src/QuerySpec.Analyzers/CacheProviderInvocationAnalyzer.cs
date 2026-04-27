using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QuerySpec.Analyzers;

/// <summary>
/// Reports <see cref="DiagnosticDescriptors.CacheProviderInvocation"/> (QSPEC0003) on every
/// invocation of <c>ICacheProvider.GetAsync&lt;T&gt;</c> or <c>ICacheProvider.SetAsync&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// Resolution uses <see cref="SymbolEqualityComparer"/> on the called member's containing type so
/// the diagnostic fires whether the receiver is typed as <c>ICacheProvider</c> or as one of its
/// shipping implementations (<c>MemoryCacheProvider</c>, <c>DistributedCacheProvider</c>,
/// <c>MultiLevelCache</c>). Concrete-receiver call sites still resolve to the interface member on
/// the explicit-interface implementation path, but the analyzer also handles the
/// non-explicit-interface case by climbing <c>IMethodSymbol.ContainingType</c> until it
/// reaches an <c>ITypeSymbol.AllInterfaces</c> entry that matches.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CacheProviderInvocationAnalyzer : DiagnosticAnalyzer
{
    private const string CacheProviderFullName = "QuerySpec.Core.Caching.ICacheProvider";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.CacheProviderInvocation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null) return;

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var cacheProvider = compilationStart.Compilation.GetTypeByMetadataName(CacheProviderFullName);
            if (cacheProvider is null)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, cacheProvider),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol cacheProviderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var name = memberAccess.Name.Identifier.ValueText;
        if (!string.Equals(name, "GetAsync", System.StringComparison.Ordinal)
            && !string.Equals(name, "SetAsync", System.StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return;
        }

        if (!IsCacheProviderMember(method, cacheProviderType))
        {
            return;
        }

        if (HasMatchingObsoleteAttribute(method))
        {
            return;
        }

        var replacement = string.Equals(name, "GetAsync", System.StringComparison.Ordinal)
            ? "TryGetAsync"
            : "SetValueAsync";
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CacheProviderInvocation,
            memberAccess.Name.Identifier.GetLocation(),
            name,
            replacement));
    }

    private static bool HasMatchingObsoleteAttribute(IMethodSymbol method)
    {
        foreach (var candidate in EnumerateCandidateMembers(method))
        {
            foreach (var attribute in candidate.GetAttributes())
            {
                var className = attribute.AttributeClass?.Name;
                if (className is null
                    || !string.Equals(className, "ObsoleteAttribute", System.StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var named in attribute.NamedArguments)
                {
                    if (string.Equals(named.Key, "DiagnosticId", System.StringComparison.Ordinal)
                        && named.Value.Value is string id
                        && string.Equals(id, DiagnosticIds.CacheProviderInvocation, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static System.Collections.Generic.IEnumerable<ISymbol> EnumerateCandidateMembers(IMethodSymbol method)
    {
        yield return method;

        foreach (var implemented in method.ExplicitInterfaceImplementations)
        {
            yield return implemented;
        }

        var containing = method.ContainingType;
        if (containing is null)
        {
            yield break;
        }

        foreach (var iface in containing.AllInterfaces)
        {
            foreach (var member in iface.GetMembers(method.Name))
            {
                if (member is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                var implementor = containing.FindImplementationForInterfaceMember(interfaceMethod);
                if (SymbolEqualityComparer.Default.Equals(implementor, method))
                {
                    yield return interfaceMethod;
                }
            }
        }
    }

    private static bool IsCacheProviderMember(IMethodSymbol method, INamedTypeSymbol cacheProviderType)
    {
        var containing = method.ContainingType;
        if (containing is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(containing.OriginalDefinition, cacheProviderType))
        {
            return true;
        }

        if (method.IsExtensionMethod)
        {
            return false;
        }

        foreach (var implemented in method.ExplicitInterfaceImplementations)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented.ContainingType.OriginalDefinition, cacheProviderType))
            {
                return true;
            }
        }

        var methodDefinition = method.OriginalDefinition;
        foreach (var iface in containing.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, cacheProviderType))
            {
                continue;
            }

            foreach (var member in iface.GetMembers(method.Name))
            {
                if (member is not IMethodSymbol interfaceMethod)
                {
                    continue;
                }

                var implementor = containing.FindImplementationForInterfaceMember(interfaceMethod);
                if (implementor is null)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(implementor.OriginalDefinition, methodDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
