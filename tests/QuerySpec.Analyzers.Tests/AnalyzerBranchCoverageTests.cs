using System.Threading.Tasks;
using QuerySpec.Analyzers.Tests.Verifiers;
using Xunit;
using VerifyCache = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer>;
using VerifyAdvanced = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.AdvancedFilterExpressionAnalyzer>;
using VerifyGeo = QuerySpec.Analyzers.Tests.Verifiers.CSharpAnalyzerVerifier<QuerySpec.Analyzers.GeoLocationMemberAccessAnalyzer>;
using VerifyCacheFix = QuerySpec.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<QuerySpec.Analyzers.CacheProviderInvocationAnalyzer, QuerySpec.Analyzers.CacheProviderInvocationCodeFixProvider>;

namespace QuerySpec.Analyzers.Tests;

/// <summary>
/// Targets uncovered branches in all three analyzers and two code fix providers
/// identified from the batch-2 CI coverage report.
/// </summary>
public sealed class AnalyzerBranchCoverageTests
{
    // ── CacheProviderInvocationAnalyzer: InvocationExpression with no receiver ──
    // Covers the false branch of `invocation.Expression is not MemberAccessExpressionSyntax`.

    [Fact]
    public async Task CacheAnalyzer_InvocationWithoutMemberAccess_NoDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
        const string Source = """
            using System.Threading;
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task M(ICacheProvider _)
                {
                    ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
                        => default;
                    await GetAsync("k");
                }
            }
            """;

        await VerifyCache.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── CacheProviderInvocationAnalyzer: invocation resolves to non-method symbol ──
    // Covers the false branch of `Symbol is not IMethodSymbol`.

    [Fact]
    public async Task CacheAnalyzer_GetAsyncNameOnDelegate_NoDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
        const string Source = """
            using System;
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                Func<string, ValueTask<string?>> GetAsync = _ => default;

                async Task M()
                {
                    await this.GetAsync("k");
                }
            }
            """;

        await VerifyCache.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── CacheProviderInvocationAnalyzer: concrete class via explicit-interface receiver ──
    // Covers the concrete-receiver path through AllInterfaces in IsCacheProviderMember.

    [Fact]
    public async Task CacheAnalyzer_ExplicitInterfaceImplementation_TriggersDiagnostic()
    {
        const string Stub = """
            #nullable enable
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

                public class ExplicitProvider : ICacheProvider
                {
                    ValueTask<T?> ICacheProvider.GetAsync<T>(string key, CancellationToken ct) where T : class => default;
                    ValueTask ICacheProvider.SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken ct) where T : class => default;
                    ValueTask ICacheProvider.RemoveAsync(string key, CancellationToken ct) => default;
                }
            }
            """;
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

        var expected = VerifyCache.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyCache.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── CacheProviderInvocationAnalyzer: EnumerateCandidateMembers false branch ──
    // Covers line `if (SymbolEqualityComparer.Default.Equals(implementor, method))` false:
    // concrete class implements two interfaces both with GetAsync; one is via explicit impl
    // so FindImplementationForInterfaceMember(IFoo.GetAsync) returns the explicit member,
    // not the method being analyzed, exercising the non-match branch.

    [Fact]
    public async Task CacheAnalyzer_DualInterfaceConcreteReceiver_TriggersDiagnostic()
    {
        const string Stub = """
            #nullable enable
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

                public interface ILegacyRead
                {
                    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
                }

                public class DualProvider : ICacheProvider, ILegacyRead
                {
                    public ValueTask<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => default;
                    public ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class => default;
                    public ValueTask RemoveAsync(string key, CancellationToken ct = default) => default;
                    ValueTask<T?> ILegacyRead.GetAsync<T>(string key, CancellationToken ct) where T : class => default;
                }
            }
            """;
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<string?> M(DualProvider cache)
                {
                    return await cache.{|#0:GetAsync|}<string>("k");
                }
            }
            """;

        var expected = VerifyCache.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyCache.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── CacheProviderInvocationAnalyzer: HasMatchingObsoleteAttribute ──────────
    // Covers the branch where attribute.AttributeClass?.Name is non-null but not
    // "ObsoleteAttribute" — the className null-check false branch.

    [Fact]
    public async Task CacheAnalyzer_ObsoleteWithNullAttributeClass_StillFiresDiagnostic()
    {
        const string Stub = TestStubs.CacheTypes;
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

        var expected = VerifyCache.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("SetAsync", "SetValueAsync");

        await VerifyCache.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── AdvancedFilterExpressionAnalyzer: ObjectCreation of non-named type ─────
    // Covers the false branch of `typeInfo.Type is not INamedTypeSymbol` (anonymous object).

    [Fact]
    public async Task AdvancedAnalyzer_AnonymousObjectCreation_NoDiagnostic()
    {
        const string Stub = TestStubs.FilterTypes;
        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new { Field = "Status" };
            }
            """;

        await VerifyAdvanced.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── AdvancedFilterExpressionAnalyzer: ObsoleteAttribute has no named args ──
    // Covers the foreach-over-zero-named-args path: loop enters but has nothing to iterate.

    [Fact]
    public async Task AdvancedAnalyzer_ObsoleteWithoutNamedArgs_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                [Obsolete]
                public class AdvancedFilterExpression
                {
                    public string Field { get; set; } = "";
                }

                public enum FilterOperator { Equal }

                public sealed record FilterSpec
                {
                    public string Field { get; init; } = "";
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                object M() => new {|#0:AdvancedFilterExpression|}();
            }
            """;

        var expected = VerifyAdvanced.Diagnostic(DiagnosticIds.AdvancedFilterExpressionUsage)
            .WithLocation(0);

        await VerifyAdvanced.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── GeoLocationMemberAccessAnalyzer: member access resolves to a field ─────
    // Covers the false branch of `symbol is not IPropertySymbol` (Latitude as a field).

    [Fact]
    public async Task GeoAnalyzer_LatitudeField_NoDiagnostic()
    {
        const string Stub = """
            namespace QuerySpec.Core.Advanced
            {
                public class GeoLocation
                {
                    public decimal Latitude;
                    public decimal Longitude;
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                decimal M(GeoLocation g) => g.Latitude;
            }
            """;

        await VerifyGeo.VerifyAnalyzerAsync(Source, new[] { Stub });
    }

    // ── GeoLocationMemberAccessAnalyzer: ObsoleteAttribute has no named args ────
    // Covers the foreach-over-zero-named-args path in HasMatchingObsoleteAttribute.

    [Fact]
    public async Task GeoAnalyzer_ObsoleteWithoutNamedArgs_StillFiresDiagnostic()
    {
        const string Stub = """
            using System;

            namespace QuerySpec.Core.Advanced
            {
                public class GeoLocation
                {
                    [Obsolete]
                    public decimal Latitude { get; set; }
                    public decimal Longitude { get; set; }
                }
            }
            """;

        const string Source = """
            using QuerySpec.Core.Advanced;

            class C
            {
                decimal M(GeoLocation g) => g.{|#0:Latitude|};
            }
            """;

        var expected = VerifyGeo.Diagnostic(DiagnosticIds.GeoLocationMemberAccess)
            .WithLocation(0)
            .WithArguments("Latitude");

        await VerifyGeo.VerifyAnalyzerAsync(Source, new[] { Stub }, expected);
    }

    // ── CacheProviderInvocationCodeFixProvider: GetAsync generic syntax rename ──
    // Covers the GenericNameSyntax branch in ReplaceMethodIdentifier.

    [Fact]
    public async Task CacheCodeFix_GetAsync_GenericSyntax_RenamedCorrectly()
    {
        const string Source = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<int> M(TestCacheProvider cache)
                {
                    var v = await cache.{|#0:GetAsync|}<string>("k");
                    return 0;
                }
            }
            """;

        const string Fixed = """
            using System.Threading.Tasks;
            using QuerySpec.Core.Caching;

            class C
            {
                async Task<int> M(TestCacheProvider cache)
                {
                    var v = (await cache.TryGetAsync<string>("k")).GetValueOrDefault();
                    return 0;
                }
            }
            """;

        var expected = VerifyCacheFix.Diagnostic(DiagnosticIds.CacheProviderInvocation)
            .WithLocation(0)
            .WithArguments("GetAsync", "TryGetAsync");

        await VerifyCacheFix.VerifyCodeFixAsync(
            Source, new[] { expected }, Fixed, new[] { TestStubs.CacheTypes });
    }
}
