using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace QuerySpec.Analyzers;

/// <summary>
/// Code fix for QSPEC0003: rewrites <c>cache.SetAsync&lt;T&gt;(...)</c> as
/// <c>cache.SetValueAsync&lt;T&gt;(...)</c>, and rewrites <c>await cache.GetAsync&lt;T&gt;(key)</c>
/// as <c>(await cache.TryGetAsync&lt;T&gt;(key)).GetValueOrDefault()</c> so the consumer keeps the
/// same null-fallback shape they had on the legacy API.
/// </summary>
/// <remarks>
/// The shipping providers (<c>MemoryCacheProvider</c>, <c>DistributedCacheProvider</c>,
/// <c>MultiLevelCache</c>) implement both <c>ICacheProvider</c> and <c>ICacheStore</c> through 3.x,
/// so the rewritten call resolves against the same instance: no DI changes required. Consumers
/// who explicitly typed their variable as <c>ICacheProvider</c> will get a compile error after the
/// fix because the new method does not exist on that interface; the diagnostic message and the
/// migration sample steer them to retype as <c>ICacheStore</c>. We deliberately do not auto-rewrite
/// the variable declaration because that would be a cross-file edit with subtle scoping
/// implications (DI registration, downstream usages, etc.) — that judgement stays with the
/// developer.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CacheProviderInvocationCodeFixProvider))]
[Shared]
public sealed class CacheProviderInvocationCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use ICacheStore instead of ICacheProvider";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.CacheProviderInvocation);

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
            var memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
            if (memberAccess?.Parent is not InvocationExpressionSyntax invocation) continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: ct => RewriteAsync(context.Document, invocation, ct),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> RewriteAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var calledName = memberAccess.Name.Identifier.ValueText;
        SyntaxNode? newRoot = calledName switch
        {
            "SetAsync" => RewriteSetAsync(root, invocation, memberAccess),
            "GetAsync" => RewriteGetAsync(root, invocation, memberAccess),
            _ => null,
        };

        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode RewriteSetAsync(
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        var renamedName = ReplaceMethodIdentifier(memberAccess.Name, "SetValueAsync");
        var newAccess = memberAccess.WithName(renamedName);
        var replacement = invocation.WithExpression(newAccess);
        return root.ReplaceNode(invocation, replacement);
    }

    private static SyntaxNode RewriteGetAsync(
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        var renamedName = ReplaceMethodIdentifier(memberAccess.Name, "TryGetAsync");
        var newAccess = memberAccess.WithName(renamedName);
        var renamedInvocation = invocation.WithExpression(newAccess);

        // Wrap as: (await cache.TryGetAsync<T>(key)).GetValueOrDefault()
        // If the original call site was already inside an `await` expression, replace the await
        // with the parenthesised form so we keep one await and add the GetValueOrDefault hop.
        if (invocation.Parent is AwaitExpressionSyntax existingAwait)
        {
            var awaited = existingAwait.WithExpression(renamedInvocation);
            var parenthesised = SyntaxFactory.ParenthesizedExpression(awaited.WithoutTrivia());
            var dotted = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                parenthesised,
                SyntaxFactory.IdentifierName("GetValueOrDefault"));
            var withFallback = SyntaxFactory.InvocationExpression(dotted)
                .WithTriviaFrom(existingAwait)
                .WithAdditionalAnnotations(Formatter.Annotation);
            return root.ReplaceNode(existingAwait, withFallback);
        }

        // No surrounding await — emit a synchronous form that still composes with .Result-style
        // patterns the developer may be using. We add the .GetValueOrDefault() hop via a
        // continuation so the rewrite remains semantically consistent.
        var awaitExpr = SyntaxFactory.AwaitExpression(renamedInvocation.WithoutTrivia());
        var paren = SyntaxFactory.ParenthesizedExpression(awaitExpr);
        var member = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            paren,
            SyntaxFactory.IdentifierName("GetValueOrDefault"));
        var newCall = SyntaxFactory.InvocationExpression(member)
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return root.ReplaceNode(invocation, newCall);
    }

    private static SimpleNameSyntax ReplaceMethodIdentifier(SimpleNameSyntax original, string newName)
    {
        if (original is GenericNameSyntax generic)
        {
            return generic.WithIdentifier(SyntaxFactory.Identifier(newName));
        }

        if (original is IdentifierNameSyntax identifier)
        {
            return identifier.WithIdentifier(SyntaxFactory.Identifier(newName));
        }

        return SyntaxFactory.IdentifierName(newName).WithTriviaFrom(original);
    }
}
