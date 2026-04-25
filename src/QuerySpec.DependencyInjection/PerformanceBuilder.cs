using System;
using Microsoft.Extensions.DependencyInjection;
using QuerySpec.Core.Monitoring;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Performance optimization builder.
/// </summary>
public class PerformanceBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new performance builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public PerformanceBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>
    /// Enables N+1 query detection.
    /// </summary>
    public PerformanceBuilder EnableN1Detection()
    {
        _services.AddSingleton<N1DetectionEngine>();
        return this;
    }

    /// <summary>Enables query result caching.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public PerformanceBuilder EnableQueryCaching() =>
        throw new NotImplementedException("PerformanceBuilder.EnableQueryCaching is not implemented.");

    /// <summary>Enables metrics collection.</summary>
    public PerformanceBuilder EnableMetrics()
    {
        _services.AddSingleton<MetricsCollector>();
        return this;
    }

    /// <summary>Enables expression tree caching and optimization.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public PerformanceBuilder OptimizeExpressions() =>
        throw new NotImplementedException("PerformanceBuilder.OptimizeExpressions is not implemented.");
}
