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
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>With*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new QuerySpec builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public QuerySpecBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>Configure caching layer (memory/redis/multi-level).</summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="CachingBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithCaching(Action<CachingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new CachingBuilder(Services);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Configure comprehensive auditing. Registers <see cref="InMemoryAuditLogger"/> as the
    /// fallback <see cref="IAuditLogger"/> only if the configure delegate did not register one;
    /// callers can wire up their own <see cref="IAuditLogger"/> inside <paramref name="configure"/>
    /// without it being silently overwritten.
    /// </summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="AuditingBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithAuditing(Action<AuditingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new AuditingBuilder(Services);
        configure(builder);
        Services.TryAddSingleton<IAuditLogger, InMemoryAuditLogger>();
        return this;
    }

    /// <summary>Configure security (encryption, masking, RLS).</summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="SecurityBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithSecurity(Action<SecurityBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SecurityBuilder(Services);
        configure(builder);
        return this;
    }

    /// <summary>Configure performance optimization and detection.</summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="PerformanceBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithPerformance(Action<PerformanceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new PerformanceBuilder(Services);
        configure(builder);
        return this;
    }

    /// <summary>Configure resilience patterns (circuit breaker, retry, rate limiting).</summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="ResilienceBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithResilience(Action<ResilienceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ResilienceBuilder(Services);
        configure(builder);
        return this;
    }

    /// <summary>Configure monitoring and observability (metrics, health checks, OpenTelemetry).</summary>
    /// <param name="configure">Delegate that mutates the inner <see cref="MonitoringBuilder"/>.</param>
    /// <returns>The same <see cref="QuerySpecBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public QuerySpecBuilder WithMonitoring(Action<MonitoringBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MonitoringBuilder(Services);
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
