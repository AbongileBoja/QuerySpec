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

    /// <summary>
    /// Enables query result caching.
    /// </summary>
    public PerformanceBuilder EnableQueryCaching()
    {
        return this;
    }

    /// <summary>
    /// Enables metrics collection.
    /// </summary>
    public PerformanceBuilder EnableMetrics()
    {
        _services.AddSingleton<MetricsCollector>();
        return this;
    }

    /// <summary>
    /// Enables expression tree caching and optimization.
    /// </summary>
    public PerformanceBuilder OptimizeExpressions()
    {
        return this;
    }
}
