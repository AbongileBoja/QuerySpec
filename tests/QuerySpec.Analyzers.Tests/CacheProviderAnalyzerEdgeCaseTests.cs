using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyAnalyzer = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer>;

namespace QuerySpec.Analyzers.Tests;

/// <summary>
/// Additional coverage for CacheProviderInvocationAnalyzer paths not reached by the
/// existing analyzer tests:
///   - No ICacheProvider type in compilation (early return)
///   - Concrete class implementing ICacheProvider via explicit interface
///   - Method symbol is null (non-method invocation)
///   - Extension method on ICacheProvider (IsExtensionMethod path in IsCacheProviderMember)
///   - GetAsync on a concrete implementor where AllInterfaces traversal is needed
/// </summary>
public sealed class CacheProviderAnalyzerEdgeCaseTests
{
    // ── No ICacheProvider in compilation — no diagnostic emitted ─────────────

    [Fact]
    public async Task NoICacheProviderInCompilation_NodiagnosticFired()
    {
        const string Source = """
            class C
            {
                void M()
                {
                    var x = 1;
                }
            }
            """;
        await VerifyAnalyzer.VerifyAnalyzerAsync(Source);
    }

    // ── Concrete implementor called directly — diagnostic via AllInterfaces traversal ──

    [Fact]
    public async Task GetAsync_ConcreteClass_DirectCall_TriggersDiagnostic()
    {
        const string Stub = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace QuerySpec.Core.Caching
            {
                public interface ICacheProvider
                {
                    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
                    ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
                    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
                }

                public class ConcreteProvider : ICacheProvider
                {
                    public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => default;
                    public ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class => default;
                    public ValueTask RemoveAsync(string key, CancellationToken ct = default) => default;
                }
            }
            """;
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(ConcreteProvider cache)
                {
                    return await cache.{|#0:GetAsync|}<string>("k");
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── SetAsync on concrete implementor — AllInterfaces path ─────────────────

    [Fact]
    public async Task SetAsync_ConcreteClass_DirectCall_TriggersDiagnostic()
    {
        const string Stub = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace QuerySpec.Core.Caching
            {
                public interface ICacheProvider
                {
                    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
                    ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
                    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
                }

                public class ConcreteProvider : ICacheProvider
                {
                    public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => default;
                    public ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class => default;
                    public ValueTask RemoveAsync(string key, CancellationToken ct = default) => default;
                }
            }
            """;
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(ConcreteProvider cache)
                {
                    await cache.{|#0:SetAsync|}("k", "v");
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("SetAsync", "SetValueAsync");

        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── Non-invocation member access (e.g. method group) — no diagnostic ────

    [Fact]
    public async Task NonInvocationMemberAccess_NoDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                void M(TestCacheProvider cache)
                {
                    _ = cache.RemoveAsync("k");
                }
            }
            """;
        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── GetAsync on unrelated interface with same name — no diagnostic ────────

    [Fact]
    public async Task GetAsync_UnrelatedInterface_NoDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
        const string Source = """
            using System.Threading.Tasks;

            interface IFoo
            {
                ValueTask<string?> GetAsync<T>(string key) where T : class;
            }

            class C
            {
                async Task<string?> M(IFoo foo)
                {
                    return await foo.GetAsync<string>("k");
                }
            }
            """;
        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── Extension method on ICacheProvider — not flagged (IsExtensionMethod branch) ──
    // When the invocation resolves to an extension method, IsCacheProviderMember
    // returns false (line 172) because the receiver type is not the containing type
    // of an extension method.

    [Fact]
    public async Task ExtensionMethod_WithGetAsyncName_NoDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            static class CacheExtensions
            {
                public static ValueTask<T?> GetAsync<T>(this ICacheStore store, string key) where T : class
                    => default;
            }

            class C
            {
                async Task<string?> M(ICacheStore store)
                {
                    return await store.GetAsync<string>("k");
                }
            }
            """;
        await VerifyAnalyzer.VerifyAnalyzerAsync(Source, new[] { Stub });
    }
}
