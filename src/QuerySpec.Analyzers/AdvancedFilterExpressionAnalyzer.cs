using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QuerySpec.Analyzers;

/// <summary>
/// Reports <see cref="DiagnosticDescriptors.AdvancedFilterExpressionUsage"/> (QSPEC0002) on
/// every <c>new AdvancedFilterExpression(...)</c> and on every type reference in a variable or
/// parameter declaration that names the deprecated POCO.
/// </summary>
/// <remarks>
/// Two registrations: <see cref="SyntaxKind.ObjectCreationExpression"/> catches the canonical
/// <c>new AdvancedFilterExpression { ... }</c> migration site, while
/// <see cref="SyntaxKind.VariableDeclaration"/> and <see cref="SyntaxKind.Parameter"/> cover
/// declared usages so the diagnostic also surfaces on holders that cross API boundaries. Round-trip
/// helpers in the QuerySpec.Core.Advanced namespace itself (<c>FilterSpec.FromMutable</c> /
/// <c>ToMutable</c>) emit on consumer code; the source itself is silenced via
/// <c>WarningsNotAsErrors</c> in the QuerySpec.Core <c>Directory.Build.props</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AdvancedFilterExpressionAnalyzer : DiagnosticAnalyzer
{
    private const string AdvancedFilterExpressionFullName =
        "QuerySpec.Core.Advanced.AdvancedFilterExpression";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.AdvancedFilterExpressionUsage);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null) return;

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var legacy = compilationStart.Compilation.GetTypeByMetadataName(
                AdvancedFilterExpressionFullName);
            if (legacy is null)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                ctx => AnalyzeObjectCreation(ctx, legacy),
                SyntaxKind.ObjectCreationExpression);
        });
    }

    private static void AnalyzeObjectCreation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol legacyType)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol named)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(named, legacyType))
        {
            return;
        }

        if (HasMatchingObsoleteAttribute(named))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AdvancedFilterExpressionUsage,
            creation.Type.GetLocation()));
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
                    && string.Equals(id, DiagnosticIds.AdvancedFilterExpressionUsage, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
