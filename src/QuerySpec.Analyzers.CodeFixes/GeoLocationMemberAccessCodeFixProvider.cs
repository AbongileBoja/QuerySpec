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
/// Code fix for QSPEC0001: rewrites <c>legacy.Latitude</c> as <c>legacy.ToGeoCoordinate().Latitude</c>
/// (and likewise for <c>Longitude</c>). The migration helper is intent-revealing, allocation-free
/// (<c>GeoCoordinate</c> is a <see langword="readonly"/> <see langword="record"/> <see langword="struct"/>),
/// and validates inputs.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeoLocationMemberAccessCodeFixProvider))]
[Shared]
public sealed class GeoLocationMemberAccessCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use GeoCoordinate instead of GeoLocation";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.GeoLocationMemberAccess);

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
            var nameSyntax = root.FindNode(diagnostic.Location.SourceSpan);
            var memberAccess = nameSyntax.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
            if (memberAccess is null) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: ct => RewriteAsync(context.Document, memberAccess, ct),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var receiver = memberAccess.Expression;
        var memberName = memberAccess.Name;

        var helperCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver.WithoutTrivia(),
                SyntaxFactory.IdentifierName("ToGeoCoordinate")));

        var replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                helperCall,
                memberName.WithoutTrivia())
            .WithTriviaFrom(memberAccess);

        var newRoot = root.ReplaceNode(memberAccess, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
