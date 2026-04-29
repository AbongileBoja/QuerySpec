using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyCodeFix = QuerySpec.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer, QuerySpec.Analyzers.CacheProviderInvocationCodeFixProvider>;

namespace QuerySpec.Analyzers.Tests;

/// <summary>
/// Edge-case paths in CacheProviderInvocationCodeFixProvider not covered by
/// the existing CacheProviderInvocationAnalyzerTests:
///   - GetAsync without a surrounding await (non-AwaitExpressionSyntax parent)
///   - Non-generic method name (IdentifierNameSyntax branch in ReplaceMethodIdentifier)
/// </summary>
public sealed class CacheProviderCodeFixEdgeCaseTests
{
    private static readonly string[] StubSources = { TestStubs.CacheTypes };

    // ── GetAsync without surrounding await rewritten via non-await path ───────

    [Fact]
    public async Task CodeFix_Rewrites_GetAsync_WithoutAwait_AddsAwaitAndGetValueOrDefault()
    {
        // When GetAsync is NOT inside an AwaitExpressionSyntax parent (i.e. the parent node is
        // NOT an AwaitExpressionSyntax), the code fix takes the fallback branch in RewriteGet.
        // The source uses an async void method so the introduced await in the fixed state is valid.
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async void M(TestCacheProvider cache)
                {
                    var t = cache.{|#0:GetAsync|}<string>("k");
                    _ = t;
                }
            }
            """;

        const string Fixed = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async void M(TestCacheProvider cache)
                {
                    var t = (await cache.TryGetAsync<string>("k")).GetValueOrDefault();
                    _ = t;
                }
            }
            """;

        var expected = VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyCodeFix.VerifyCodeFixAsync(Source, new[] { expected }, Fixed, StubSources);
    }

    // ── SetAsync using non-generic call (IdentifierNameSyntax, not GenericNameSyntax) ──

    [Fact]
    public async Task CodeFix_Rewrites_SetAsync_NonGenericCall_To_SetValueAsync()
    {
        // Exercises the IdentifierNameSyntax branch in ReplaceMethodIdentifier
        // (a non-generic call: cache.SetAsync("k", "v") resolved via extension etc.)
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(TestCacheProvider cache)
                {
                    await cache.{|#0:SetAsync|}("k", "v");
                }
            }
            """;

        const string Fixed = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(TestCacheProvider cache)
                {
                    await cache.SetValueAsync("k", "v");
                }
            }
            """;

        var expected = VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("SetAsync", "SetValueAsync");

        await VerifyCodeFix.VerifyCodeFixAsync(Source, new[] { expected }, Fixed, StubSources);
    }

    // ── FixAll: GetAsync and SetAsync mixed in same document ─────────────────

    [Fact]
    public async Task FixAll_RewritesMixedGetAndSet_InDocument()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task Multi(TestCacheProvider cache)
                {
                    await cache.{|#0:SetAsync|}("a", "x");
                    var r = await cache.{|#1:GetAsync|}<string>("b");
                }
            }
            """;

        const string Fixed = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task Multi(TestCacheProvider cache)
                {
                    await cache.SetValueAsync("a", "x");
                    var r = (await cache.TryGetAsync<string>("b")).GetValueOrDefault();
                }
            }
            """;

        var diagnostics = new[]
        {
            VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation).WithLocation(0).WithArguments("SetAsync", "SetValueAsync"),
            VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation).WithLocation(1).WithArguments("GetAsync", "TryGetAsync"),
        };

        await VerifyCodeFix.VerifyCodeFixAsync(Source, diagnostics, Fixed, StubSources);
    }

    // ── GetFixAllProvider returns non-null BatchFixer ─────────────────────────

    [Fact]
    public void GetFixAllProvider_ReturnsBatchFixer()
    {
        var provider = new QuerySpec.Analyzers.CacheProviderInvocationCodeFixProvider();
        var fixAll = provider.GetFixAllProvider();
        Assert.NotNull(fixAll);
    }

    // ── FixableDiagnosticIds contains QSPEC0003 ────────────────────────────────

    [Fact]
    public void FixableDiagnosticIds_ContainsQSPEC0003()
    {
        var provider = new QuerySpec.Analyzers.CacheProviderInvocationCodeFixProvider();
        Assert.Contains(DiagnosticIds.CacheProviderInvocation, provider.FixableDiagnosticIds);
    }
}
