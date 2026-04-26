using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Auditing;
using QuerySpec.Core.Caching;
using QuerySpec.Core.Monitoring;
using QuerySpec.Core.Resilience;
using QuerySpec.Core.Security;
using QuerySpec.DependencyInjection;
using Xunit;

namespace QuerySpec.Core.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the top-level <see cref="QuerySpecBuilder"/> / <c>AddQuerySpec</c> entry
/// point. Verifies the fluent API composes correctly and that top-level configuration is
/// dispatched to the right sub-builders.
/// </summary>
public class QuerySpecBuilderTests
{
    [Fact]
    public void AddQuerySpec_ReturnsBuilder_AndInvokesConfigure()
    {
        var services = new ServiceCollection();
        var invoked = false;
        var builder = services.AddQuerySpec(_ => invoked = true);

        Assert.True(invoked);
        Assert.NotNull(builder);
        Assert.IsType<QuerySpecBuilder>(builder);
    }

    [Fact]
    public void WithCaching_MemoryCache_RegistersMemoryCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q.WithCaching(c => c.UseMemoryCache()));

        using var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<ICacheProvider>();

        Assert.IsType<MemoryCacheProvider>(cache);
    }

    [Fact]
    public void WithCaching_MultiLevel_RegistersMultiLevelCache()
    {
        var services = new ServiceCollection();
        // MultiLevel needs IDistributedCache; use in-memory distributed impl.
        services.AddDistributedMemoryCache();
        services.AddQuerySpec(q => q.WithCaching(c => c.UseMultiLevel()));

        using var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<ICacheProvider>();

        Assert.IsType<MultiLevelCache>(cache);
    }

    [Fact]
    public void WithResilience_ConfiguresAllPolicies()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q.WithResilience(r => r
            .UseCircuitBreaker(failureThreshold: 3, openTimeout: TimeSpan.FromSeconds(5))
            .UseRetryPolicy(maxRetries: 4, exponentialBackoff: true)
            .UseRateLimiting(tokensPerSecond: 250)
            .UseBulkhead(maxConcurrentRequests: 8)));

        using var sp = services.BuildServiceProvider();
        var policy = sp.GetRequiredService<ResiliencePolicy>();

        Assert.NotNull(policy.CircuitBreaker);
        Assert.Equal(3, policy.CircuitBreaker!.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.CircuitBreaker.OpenTimeout);

        Assert.NotNull(policy.RetryPolicy);
        Assert.Equal(4, policy.RetryPolicy!.MaxRetries);
        Assert.True(policy.RetryPolicy.UseExponentialBackoff);

        Assert.NotNull(policy.RateLimiter);
        Assert.Equal(250, policy.RateLimiter!.TokensPerSecond);

        Assert.NotNull(policy.Bulkhead);
    }

    [Fact]
    public void WithPerformance_RegistersN1AndMetrics()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q.WithPerformance(p => p
            .EnableN1Detection()
            .EnableMetrics()));

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<N1DetectionEngine>());
        Assert.NotNull(sp.GetRequiredService<MetricsCollector>());
    }

    [Fact]
    public void WithSecurity_RegistersConfiguredServices()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q.WithSecurity(s => s
            // Base64-encoded 32-byte (256-bit) AES key.
            .EnableFieldEncryption(Convert.ToBase64String(new byte[32]))
            .EnableDataMasking()
            .EnableRowLevelSecurity()
            .EnableDynamicPermissions()));

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IEncryptionProvider>());
        Assert.NotNull(sp.GetRequiredService<DataMaskingEngine>());
        Assert.NotNull(sp.GetRequiredService<RowLevelSecurityEngine>());
        Assert.NotNull(sp.GetRequiredService<DynamicPermissionEvaluator>());
    }

    [Fact]
    public void WithAuditing_RegistersInMemoryAuditLogger()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q.WithAuditing(_ => { }));

        using var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<IAuditLogger>();

        Assert.IsType<InMemoryAuditLogger>(logger);
    }

    /// <summary>
    /// A caller-supplied <see cref="IAuditLogger"/> registered before or inside the
    /// <c>WithAuditing</c> configure delegate must be preserved. The default
    /// <see cref="InMemoryAuditLogger"/> registration is a fallback only.
    /// </summary>
    [Fact]
    public void WithAuditing_DoesNotOverride_CallerSuppliedAuditLogger()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogger, CustomAuditLogger>();

        services.AddQuerySpec(q => q.WithAuditing(_ => { }));

        using var sp = services.BuildServiceProvider();
        Assert.IsType<CustomAuditLogger>(sp.GetRequiredService<IAuditLogger>());
    }

    private sealed class CustomAuditLogger : IAuditLogger
    {
        public System.Threading.Tasks.Task LogQueryAsync(AuditLogEntry entry) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task LogChangeAsync(FieldChange change) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<AuditLogEntry?> GetAuditAsync(string id) => System.Threading.Tasks.Task.FromResult<AuditLogEntry?>(null);
        public System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId) => System.Threading.Tasks.Task.FromResult(System.Linq.Enumerable.Empty<AuditLogEntry>());
        public System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, System.DateTime? since = null) => System.Threading.Tasks.Task.FromResult(System.Linq.Enumerable.Empty<AuditLogEntry>());
        public System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, System.DateTime? since = null) => System.Threading.Tasks.Task.FromResult(System.Linq.Enumerable.Empty<AuditLogEntry>());
        public System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<AuditLogEntry>> GetComplianceReportAsync(System.DateTime from, System.DateTime to) => System.Threading.Tasks.Task.FromResult(System.Linq.Enumerable.Empty<AuditLogEntry>());
        public System.Threading.Tasks.Task PurgeOldLogsAsync(System.TimeSpan olderThan) => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void WithMonitoring_StubsThrowAtConfigTime()
    {
        var services = new ServiceCollection();
#pragma warning disable CS0618
        Assert.Throws<NotImplementedException>(() =>
            services.AddQuerySpec(q => q.WithMonitoring(m => m.EnableOpenTelemetry())));
#pragma warning restore CS0618
    }

    [Fact]
    public void ChainedConfiguration_DoesNotConflict()
    {
        var services = new ServiceCollection();
        services.AddQuerySpec(q => q
            .WithCaching(c => c.UseMemoryCache())
            .WithResilience(r => r.UseCircuitBreaker(5, TimeSpan.FromSeconds(10)))
            .WithPerformance(p => p.EnableN1Detection())
            .WithAuditing(_ => { })
            .WithSecurity(s => s.EnableRowLevelSecurity()));

        using var sp = services.BuildServiceProvider();

        Assert.IsType<MemoryCacheProvider>(sp.GetRequiredService<ICacheProvider>());
        Assert.NotNull(sp.GetRequiredService<ResiliencePolicy>().CircuitBreaker);
        Assert.NotNull(sp.GetRequiredService<N1DetectionEngine>());
        Assert.IsType<InMemoryAuditLogger>(sp.GetRequiredService<IAuditLogger>());
        Assert.NotNull(sp.GetRequiredService<RowLevelSecurityEngine>());
    }
}
