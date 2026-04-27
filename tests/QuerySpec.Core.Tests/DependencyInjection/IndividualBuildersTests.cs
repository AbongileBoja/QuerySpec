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
    // ---------- Services getter (third-party Use* extension story) ----------

    /// <summary>
    /// Every builder must expose the underlying <see cref="IServiceCollection"/> via
    /// <c>Services</c> so third-party packages can author <c>Use*</c> extensions that
    /// compose with the fluent QuerySpec API. This test asserts the property returns the
    /// same instance that was passed to the constructor for all 7 builders.
    /// </summary>
    [Fact]
    public void Builders_Services_ReturnsSameInstance_AsConstructorArgument()
    {
        var services = new ServiceCollection();

        Assert.Same(services, new QuerySpecBuilder(services).Services);
        Assert.Same(services, new CachingBuilder(services).Services);
        Assert.Same(services, new AuditingBuilder(services).Services);
        Assert.Same(services, new SecurityBuilder(services).Services);
        Assert.Same(services, new PerformanceBuilder(services).Services);
        Assert.Same(services, new ResilienceBuilder(services).Services);
        Assert.Same(services, new MonitoringBuilder(services).Services);
    }

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

    /// <summary>
    /// Regression: <see cref="CachingBuilder.UseMemoryCache"/> must register a single
    /// <see cref="MemoryCacheProvider"/> singleton against both <see cref="ICacheProvider"/>
    /// (legacy) and <see cref="ICacheStore"/> (new) so consumers may inject either contract.
    /// </summary>
    [Fact]
    public void CachingBuilder_UseMemoryCache_RegistersBothProviderAndStoreAgainstSameInstance()
    {
        var services = new ServiceCollection();
        new CachingBuilder(services).UseMemoryCache();
        using var sp = services.BuildServiceProvider();
        var legacy = sp.GetRequiredService<ICacheProvider>();
        var store = sp.GetRequiredService<ICacheStore>();
        Assert.Same(legacy, store);
        Assert.IsType<MemoryCacheProvider>(legacy);
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
        Assert.Contains(services, d => d.ServiceType == typeof(ICacheStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IDistributedCache));
    }

    /// <summary>
    /// Regression: <see cref="CachingBuilder.UseDistributedRedis"/> must register both
    /// <see cref="ICacheProvider"/> and <see cref="ICacheStore"/> against the same
    /// <see cref="DistributedCacheProvider"/> singleton.
    /// </summary>
    [Fact]
    public void CachingBuilder_UseDistributedRedis_RegistersBothProviderAndStoreAgainstSameInstance()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache(); // stand-in for Redis backend
        new CachingBuilder(services).UseDistributedRedis("localhost:6379");

        // The Redis registration overrides IDistributedCache; substitute the in-memory one back.
        var redisDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        if (redisDescriptor is not null) services.Remove(redisDescriptor);
        services.AddDistributedMemoryCache();

        using var sp = services.BuildServiceProvider();
        var legacy = sp.GetRequiredService<ICacheProvider>();
        var store = sp.GetRequiredService<ICacheStore>();
        Assert.Same(legacy, store);
        Assert.IsType<DistributedCacheProvider>(legacy);
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

    /// <summary>
    /// Regression: <see cref="CachingBuilder.UseMultiLevel"/> must register both
    /// <see cref="ICacheProvider"/> and <see cref="ICacheStore"/> against the same
    /// <see cref="MultiLevelCache"/> singleton.
    /// </summary>
    [Fact]
    public void CachingBuilder_UseMultiLevel_RegistersBothProviderAndStoreAgainstSameInstance()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        new CachingBuilder(services).UseMultiLevel();
        using var sp = services.BuildServiceProvider();
        var legacy = sp.GetRequiredService<ICacheProvider>();
        var store = sp.GetRequiredService<ICacheStore>();
        Assert.Same(legacy, store);
        Assert.IsType<MultiLevelCache>(legacy);
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

    // ---------- AuditingBuilder ----------

    [Fact]
    public void AuditingBuilder_AloneDoesNotRegisterLogger()
    {
        var services = new ServiceCollection();
        _ = new AuditingBuilder(services);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IAuditLogger));
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
