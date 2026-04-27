using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyAnalyzer = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer>;
using VerifyCodeFix = QuerySpec.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer, QuerySpec.Analyzers.CacheProviderInvocationCodeFixProvider>;

namespace QuerySpec.Analyzers.Tests;

public sealed class CacheProviderInvocationAnalyzerTests
{
    private static readonly string[] StubSources = { TestStubs.CacheTypes };

    [Fact]
    public async Task GetAsync_On_ICacheProvider_Triggers_Diagnostic()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(ICacheProvider cache)
                {
                    return await cache.{|#0:GetAsync|}<string>("k");
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task SetAsync_On_ICacheProvider_Triggers_Diagnostic()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(ICacheProvider cache)
                {
                    await cache.{|#0:SetAsync|}("k", "v");
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("SetAsync", "SetValueAsync");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources, expected);
    }

    [Fact]
    public async Task TryGetAsync_On_ICacheStore_Does_Not_Trigger()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(ICacheStore store)
                {
                    var hit = await store.TryGetAsync<int>("k");
                    await store.SetValueAsync("k", 1);
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task GetAsync_On_Unrelated_Type_Does_Not_Trigger()
    {
        const string Source = """
            using System.Threading.Tasks;

            interface IOther
            {
                Task<T?> GetAsync<T>(string key) where T : class;
            }

            class C
            {
                async Task<string?> M(IOther o) => await o.GetAsync<string>("k");
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source);
    }

    [Fact]
    public async Task Suppression_Pragma_Silences_Diagnostic()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(ICacheProvider cache)
                {
            #pragma warning disable QSPEC0003
                    return await cache.GetAsync<string>("k");
            #pragma warning restore QSPEC0003
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, StubSources);
    }

    [Fact]
    public async Task Analyzer_Suppresses_When_Method_Already_Has_Matching_Obsolete_DiagnosticId()
    {
        const string ObsoleteStub = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace QuerySpec.Core.Caching
            {
                public interface ICacheProvider
                {
                    [Obsolete("Use ICacheStore.TryGetAsync<T>", DiagnosticId = "QSPEC0003")]
                    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
                }
            }
            """;
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
            #pragma warning disable QSPEC0003
                async Task<string?> M(ICacheProvider cache)
                    => await cache.GetAsync<string>("k");
            #pragma warning restore QSPEC0003
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { ObsoleteStub });
    }

    [Fact]
    public void HelpLinkUri_Points_At_GitHub_Diagnostic_Reference()
    {
        var descriptor = DiagnosticDescriptors.CacheProviderInvocation;
        Assert.Equal(
            "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/QSPEC0003.md",
            descriptor.HelpLinkUri);
        Assert.Equal("QSPEC0003", descriptor.Id);
    }

    [Fact]
    public async Task CodeFix_Rewrites_SetAsync_To_SetValueAsync()
    {
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

    [Fact]
    public async Task CodeFix_Rewrites_GetAsync_With_Await_To_TryGetAsync_Plus_GetValueOrDefault()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(TestCacheProvider cache)
                {
                    return await cache.{|#0:GetAsync|}<string>("k");
                }
            }
            """;

        const string Fixed = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(TestCacheProvider cache)
                {
                    return (await cache.TryGetAsync<string>("k")).GetValueOrDefault();
                }
            }
            """;

        var expected = VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyCodeFix.VerifyCodeFixAsync(Source, new[] { expected }, Fixed, StubSources);
    }

    [Fact]
    public async Task FixAll_Rewrites_Multiple_Cache_Calls_In_Document()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task Multi(TestCacheProvider cache)
                {
                    await cache.{|#0:SetAsync|}("a", "x");
                    await cache.{|#1:SetAsync|}("b", "y");
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
                    await cache.SetValueAsync("b", "y");
                }
            }
            """;

        var diagnostics = new[]
        {
            VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation).WithLocation(0).WithArguments("SetAsync", "SetValueAsync"),
            VerifyCodeFix.Diagnostic(DiagnosticIds.CacheProviderInvocation).WithLocation(1).WithArguments("SetAsync", "SetValueAsync"),
        };

        await VerifyCodeFix.VerifyCodeFixAsync(Source, diagnostics, Fixed, StubSources);
    }
}
