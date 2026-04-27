using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Monitoring;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Performance optimization builder.
/// </summary>
public class PerformanceBuilder
{
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>Use*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new performance builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public PerformanceBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Enables N+1 query detection.
    /// </summary>
    /// <returns>The same <see cref="PerformanceBuilder"/> for fluent chaining.</returns>
    public PerformanceBuilder EnableN1Detection()
    {
        Services.AddSingleton<N1DetectionEngine>();
        return this;
    }

    /// <summary>Enables query result caching.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Use WithCaching(c => c.UseMemoryCache()) and compose your query path with the resulting ICacheProvider instead. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public PerformanceBuilder EnableQueryCaching() =>
        throw new NotImplementedException("PerformanceBuilder.EnableQueryCaching is not implemented.");

    /// <summary>Enables metrics collection.</summary>
    /// <returns>The same <see cref="PerformanceBuilder"/> for fluent chaining.</returns>
    public PerformanceBuilder EnableMetrics()
    {
        Services.AddSingleton<MetricsCollector>();
        return this;
    }

    /// <summary>Enables expression tree caching and optimization.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Expression compilation is already handled by EF Core / IQueryable providers; remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public PerformanceBuilder OptimizeExpressions() =>
        throw new NotImplementedException("PerformanceBuilder.OptimizeExpressions is not implemented.");
}
