using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Auditing;
using QuerySpec.Core.Caching;
using QuerySpec.Core.Monitoring;
using QuerySpec.Core.Resilience;
using QuerySpec.Core.Security;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Fluent builder API for configuring QuerySpec.
/// </summary>
public class QuerySpecBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new QuerySpec builder.</summary>
    public QuerySpecBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configure caching layer (memory/redis/multi-level).
    /// </summary>
    public QuerySpecBuilder WithCaching(Action<CachingBuilder> configure)
    {
        var builder = new CachingBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure comprehensive auditing.
    /// </summary>
    public QuerySpecBuilder WithAuditing(Action<AuditingBuilder> configure)
    {
        var builder = new AuditingBuilder(_services);
        configure(builder);
        _services.AddSingleton<IAuditLogger>(sp => new InMemoryAuditLogger());
        return this;
    }

    /// <summary>
    /// Configure security (encryption, masking, RLS).
    /// </summary>
    public QuerySpecBuilder WithSecurity(Action<SecurityBuilder> configure)
    {
        var builder = new SecurityBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure performance optimization and detection.
    /// </summary>
    public QuerySpecBuilder WithPerformance(Action<PerformanceBuilder> configure)
    {
        var builder = new PerformanceBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure resilience patterns (circuit breaker, retry, rate limiting).
    /// </summary>
    public QuerySpecBuilder WithResilience(Action<ResilienceBuilder> configure)
    {
        var builder = new ResilienceBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure monitoring and observability (metrics, health checks, OpenTelemetry).
    /// </summary>
    public QuerySpecBuilder WithMonitoring(Action<MonitoringBuilder> configure)
    {
        var builder = new MonitoringBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure plugin system.
    /// </summary>
    public QuerySpecBuilder WithPlugins(Action<PluginBuilder> configure)
    {
        var builder = new PluginBuilder(_services);
        configure(builder);
        return this;
    }
}

/// <summary>
/// Extension method for AddQuerySpec fluent API.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds QuerySpec services to the service collection.</summary>
    public static QuerySpecBuilder AddQuerySpec(
        this IServiceCollection services,
        Action<QuerySpecBuilder> configure)
    {
        var builder = new QuerySpecBuilder(services);
        configure(builder);
        return builder;
    }
}
