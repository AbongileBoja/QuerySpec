using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QuerySpec.Analyzers;

/// <summary>
/// Reports <see cref="DiagnosticDescriptors.GeoLocationMemberAccess"/> (QSPEC0001) on every read
/// of <c>QuerySpec.Core.Advanced.GeoLocation.Latitude</c> or <c>.Longitude</c> when the legacy
/// member is not already carrying an <see cref="System.ObsoleteAttribute"/> with the matching
/// <c>DiagnosticId</c>.
/// </summary>
/// <remarks>
/// Detection runs against the semantic model — never against syntax text — so <c>using</c>
/// aliases, fully-qualified usages, and inherited member access all resolve to the same symbol
/// comparison. The analyzer scopes its registration to <see cref="SyntaxKind.SimpleMemberAccessExpression"/>
/// only, so per-compilation cost is bounded by that node count rather than by every syntax node.
/// In normal QuerySpec consumption the property already carries
/// <c>[Obsolete(DiagnosticId = "QSPEC0001")]</c>, so the compiler emits the diagnostic and the
/// analyzer suppresses the duplicate; this analyzer remains the backstop for forks or pre-3.1
/// copies of the type and is the registration point the QSPEC0001 code fix targets.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeoLocationMemberAccessAnalyzer : DiagnosticAnalyzer
{
    private const string GeoLocationFullName = "QuerySpec.Core.Advanced.GeoLocation";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.GeoLocationMemberAccess);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null) return;

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var geoLocation = compilationStart.Compilation.GetTypeByMetadataName(GeoLocationFullName);
            if (geoLocation is null)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                ctx => Analyze(ctx, geoLocation),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol geoLocationType)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var name = memberAccess.Name.Identifier.ValueText;

        if (!string.Equals(name, "Latitude", System.StringComparison.Ordinal)
            && !string.Equals(name, "Longitude", System.StringComparison.Ordinal))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol property)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(property.ContainingType, geoLocationType))
        {
            return;
        }

        if (HasMatchingObsoleteAttribute(property))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.GeoLocationMemberAccess,
            memberAccess.Name.GetLocation(),
            name));
    }

    private static bool HasMatchingObsoleteAttribute(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
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
                    && string.Equals(id, DiagnosticIds.GeoLocationMemberAccess, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
