using System;
using System.Linq;
using Microsoft.Extensions.Caching.Distributed;
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
/// Unit tests for each individual DI sub-builder. These complement
/// <see cref="QuerySpecBuilderTests"/> by exercising every method on each
/// sub-builder — including the fluent no-ops — to guarantee the public API
/// surface is stable and chainable.
/// </summary>
public class IndividualBuildersTests
{
    // ---------- CachingBuilder ----------

    [Fact]
    public void CachingBuilder_UseMemoryCache_RegistersProviderAndIMemoryCache()
    {
        var services = new ServiceCollection();
        var result = new CachingBuilder(services).UseMemoryCache();

        Assert.IsType<CachingBuilder>(result);
        using var sp = services.BuildServiceProvider();
        Assert.IsType<MemoryCacheProvider>(sp.GetRequiredService<ICacheProvider>());
    }

    [Fact]
    public void CachingBuilder_UseDistributedRedis_RegistersDistributedProvider()
    {
        var services = new ServiceCollection();
        var builder = new CachingBuilder(services);

        var result = builder.UseDistributedRedis("localhost:6379");

        Assert.Same(builder, result);
        // We can't actually connect to Redis; assert the registration descriptors exist.
        Assert.Contains(services, d => d.ServiceType == typeof(ICacheProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IDistributedCache));
    }

    [Fact]
    public void CachingBuilder_UseMultiLevel_RegistersBothLevels()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        new CachingBuilder(services).UseMultiLevel();

        using var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<ICacheProvider>();
        Assert.IsType<MultiLevelCache>(cache);
        Assert.NotNull(sp.GetRequiredService<MemoryCacheProvider>());
        Assert.NotNull(sp.GetRequiredService<DistributedCacheProvider>());
    }

    [Fact]
    public void CachingBuilder_EnableCompressionForLarge_IsFluent()
    {
        var builder = new CachingBuilder(new ServiceCollection());
        Assert.Same(builder, builder.EnableCompressionForLarge(4096));
    }

    // ---------- ResilienceBuilder ----------

    [Fact]
    public void ResilienceBuilder_RegistersPolicyOnConstruction()
    {
        var services = new ServiceCollection();
        _ = new ResilienceBuilder(services);
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<ResiliencePolicy>());
    }

    [Fact]
    public void ResilienceBuilder_EachMethod_ReturnsSameInstance_AndConfiguresPolicy()
    {
        var services = new ServiceCollection();
        var builder = new ResilienceBuilder(services);

        Assert.Same(builder, builder.UseCircuitBreaker(7, TimeSpan.FromSeconds(2)));
        Assert.Same(builder, builder.UseRetryPolicy(3, exponentialBackoff: false));
        Assert.Same(builder, builder.UseRateLimiting(100, TimeSpan.FromSeconds(1)));
        Assert.Same(builder, builder.UseBulkhead(4));

        using var sp = services.BuildServiceProvider();
        var policy = sp.GetRequiredService<ResiliencePolicy>();

        Assert.Equal(7, policy.CircuitBreaker!.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(2), policy.CircuitBreaker.OpenTimeout);
        Assert.Equal(3, policy.RetryPolicy!.MaxRetries);
        Assert.False(policy.RetryPolicy.UseExponentialBackoff);
        Assert.Equal(100, policy.RateLimiter!.TokensPerSecond);
        Assert.NotNull(policy.Bulkhead);
    }

    [Fact]
    public void ResilienceBuilder_UseRateLimiting_WithoutWindow_StillRegisters()
    {
        var services = new ServiceCollection();
        new ResilienceBuilder(services).UseRateLimiting(50);
        using var sp = services.BuildServiceProvider();
        Assert.Equal(50, sp.GetRequiredService<ResiliencePolicy>().RateLimiter!.TokensPerSecond);
    }

    // ---------- PerformanceBuilder ----------

    [Fact]
    public void PerformanceBuilder_EnableN1Detection_RegistersEngine()
    {
        var services = new ServiceCollection();
        var builder = new PerformanceBuilder(services);

        Assert.Same(builder, builder.EnableN1Detection());

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<N1DetectionEngine>());
    }

    [Fact]
    public void PerformanceBuilder_EnableMetrics_RegistersSingleton()
    {
        var services = new ServiceCollection();
        new PerformanceBuilder(services).EnableMetrics();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<MetricsCollector>();
        var b = sp.GetRequiredService<MetricsCollector>();
        Assert.Same(a, b);
    }

    [Fact]
    public void PerformanceBuilder_EnableQueryCaching_IsFluent()
    {
        var builder = new PerformanceBuilder(new ServiceCollection());
        Assert.Same(builder, builder.EnableQueryCaching());
    }

    [Fact]
    public void PerformanceBuilder_OptimizeExpressions_IsFluent()
    {
        var builder = new PerformanceBuilder(new ServiceCollection());
        Assert.Same(builder, builder.OptimizeExpressions());
    }

    // ---------- SecurityBuilder ----------

    [Fact]
    public void SecurityBuilder_EnableFieldEncryption_RegistersProvider()
    {
        var services = new ServiceCollection();
        var builder = new SecurityBuilder(services);
        var key = Convert.ToBase64String(new byte[32]);

        Assert.Same(builder, builder.EnableFieldEncryption(key));

        using var sp = services.BuildServiceProvider();
        Assert.IsType<AesGcmEncryptionProvider>(sp.GetRequiredService<IEncryptionProvider>());
        Assert.IsType<AesGcmEncryptionProvider>(sp.GetRequiredService<IAuthenticatedEncryptionProvider>());
    }

    [Fact]
    public void SecurityBuilder_EnableDataMasking_RegistersEngine()
    {
        var services = new ServiceCollection();
        new SecurityBuilder(services).EnableDataMasking();
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<DataMaskingEngine>());
    }

    [Fact]
    public void SecurityBuilder_EnableRowLevelSecurity_RegistersEngine()
    {
        var services = new ServiceCollection();
        new SecurityBuilder(services).EnableRowLevelSecurity();
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<RowLevelSecurityEngine>());
    }

    [Fact]
    public void SecurityBuilder_EnableDynamicPermissions_RegistersEvaluator()
    {
        var services = new ServiceCollection();
        new SecurityBuilder(services).EnableDynamicPermissions();
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<DynamicPermissionEvaluator>());
    }

    [Fact]
    public void SecurityBuilder_RotateKeysEvery_IsFluent()
    {
        var builder = new SecurityBuilder(new ServiceCollection());
        Assert.Same(builder, builder.RotateKeysEvery(30));
    }

    // ---------- AuditingBuilder ----------

    [Fact]
    public void AuditingBuilder_AllMethods_ReturnSameInstance()
    {
        var builder = new AuditingBuilder(new ServiceCollection());

        Assert.Same(builder, builder.LogAllQueries());
        Assert.Same(builder, builder.TrackChanges());
        Assert.Same(builder, builder.EnableEncryption());
        Assert.Same(builder, builder.UseDatabase("Server=."));
        Assert.Same(builder, builder.RetentionDays(60));
    }

    [Fact]
    public void AuditingBuilder_AloneDoesNotRegisterLogger()
    {
        // The IAuditLogger registration is performed by QuerySpecBuilder.WithAuditing,
        // not the AuditingBuilder itself. Guard against accidental coupling.
        var services = new ServiceCollection();
        new AuditingBuilder(services).LogAllQueries().TrackChanges();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IAuditLogger));
    }

    // ---------- MonitoringBuilder ----------

    [Fact]
    public void MonitoringBuilder_AllMethods_ReturnSameInstance_AndRegisterNothing()
    {
        var services = new ServiceCollection();
        var builder = new MonitoringBuilder(services);

        Assert.Same(builder, builder.EnableOpenTelemetry());
        Assert.Same(builder, builder.EnableHealthChecks());
        Assert.Same(builder, builder.EnableDashboard());
        Assert.Same(builder, builder.EnablePrometheus());

        Assert.Empty(services);
    }

    // ---------- PluginBuilder ----------

    [Fact]
    public void PluginBuilder_AllMethods_ReturnSameInstance_AndRegisterNothing()
    {
        var services = new ServiceCollection();
        var builder = new PluginBuilder(services);

        Assert.Same(builder, builder.LoadFromDirectory("/tmp/plugins"));
        Assert.Same(builder, builder.EnableHotReload());
        Assert.Same(builder, builder.RegisterPlugin(typeof(string)));

        Assert.Empty(services);
    }

    // ---------- ServiceCollectionExtensions ----------

    [Fact]
    public void AddQuerySpec_Throws_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddQuerySpec(null!));
    }

    [Fact]
    public void AddQuerySpec_Throws_WhenServicesIsNull()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddQuerySpec(_ => { }));
    }

    [Theory]
    [MemberData(nameof(BuilderNullCtorCases))]
    public void Builder_Constructors_Throw_OnNullServices(Func<IServiceCollection, object> factory)
    {
        Assert.Throws<ArgumentNullException>(() => factory(null!));
    }

    public static System.Collections.Generic.IEnumerable<object[]> BuilderNullCtorCases() => new[]
    {
        new object[] { (Func<IServiceCollection, object>)(s => new QuerySpecBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new CachingBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new ResilienceBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new SecurityBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new PerformanceBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new AuditingBuilder(s)) },
        new object[] { (Func<IServiceCollection, object>)(s => new MonitoringBuilder(s)) },
    };

    [Fact]
    public void QuerySpecBuilder_With_Methods_Throw_OnNullConfigure()
    {
        var b = new QuerySpecBuilder(new ServiceCollection());
        Assert.Throws<ArgumentNullException>(() => b.WithCaching(null!));
        Assert.Throws<ArgumentNullException>(() => b.WithAuditing(null!));
        Assert.Throws<ArgumentNullException>(() => b.WithSecurity(null!));
        Assert.Throws<ArgumentNullException>(() => b.WithPerformance(null!));
        Assert.Throws<ArgumentNullException>(() => b.WithResilience(null!));
        Assert.Throws<ArgumentNullException>(() => b.WithMonitoring(null!));
    }

    [Fact]
    public void AddQuerySpec_WithEmptyConfigure_DoesNotRegisterAnything()
    {
        var services = new ServiceCollection();
        var builder = services.AddQuerySpec(_ => { });

        Assert.NotNull(builder);
        Assert.Empty(services);
    }
}
