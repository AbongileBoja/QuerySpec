using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QuerySpec.Analyzers;

/// <summary>
/// Code fix for QSPEC0002: rewrites <c>new AdvancedFilterExpression { ... }</c> as
/// <c>new FilterSpec { ... }</c>. The two types share property names so the object initializer
/// transfers verbatim; the new type's accessors are <see langword="init"/> rather than
/// <see langword="set"/>, which is the migration's central guarantee.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AdvancedFilterExpressionCodeFixProvider))]
[Shared]
public sealed class AdvancedFilterExpressionCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use FilterSpec instead of AdvancedFilterExpression";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.AdvancedFilterExpressionUsage);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var creation = node.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
            if (creation is null) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: ct => RewriteAsync(context.Document, creation, ct),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        ObjectCreationExpressionSyntax creation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var newTypeName = SyntaxFactory.IdentifierName("FilterSpec")
            .WithTriviaFrom(creation.Type);

        var replacement = creation.WithType(newTypeName);

        var newRoot = root.ReplaceNode(creation, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
