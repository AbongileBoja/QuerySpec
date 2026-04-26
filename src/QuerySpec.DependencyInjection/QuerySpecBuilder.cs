using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public QuerySpecBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>Configure caching layer (memory/redis/multi-level).</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithCaching(Action<CachingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new CachingBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure comprehensive auditing. Registers <see cref="InMemoryAuditLogger"/> as the
    /// fallback <see cref="IAuditLogger"/> only if the configure delegate did not register one;
    /// callers can wire up their own <see cref="IAuditLogger"/> inside <paramref name="configure"/>
    /// without it being silently overwritten.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithAuditing(Action<AuditingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new AuditingBuilder(_services);
        configure(builder);
        _services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();
        return this;
    }

    /// <summary>Configure security (encryption, masking, RLS).</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithSecurity(Action<SecurityBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SecurityBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>Configure performance optimization and detection.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithPerformance(Action<PerformanceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new PerformanceBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>Configure resilience patterns (circuit breaker, retry, rate limiting).</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithResilience(Action<ResilienceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ResilienceBuilder(_services);
        configure(builder);
        return this;
    }

    /// <summary>Configure monitoring and observability (metrics, health checks, OpenTelemetry).</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithMonitoring(Action<MonitoringBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MonitoringBuilder(_services);
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
    /// <param name="services">The service collection to register against.</param>
    /// <param name="configure">Configuration delegate.</param>
    /// <returns>The constructed <see cref="QuerySpecBuilder"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static QuerySpecBuilder AddQuerySpec(
        this IServiceCollection services,
        Action<QuerySpecBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new QuerySpecBuilder(services);
        configure(builder);
        return builder;
    }
}
