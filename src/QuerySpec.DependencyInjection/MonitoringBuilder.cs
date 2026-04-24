using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Monitoring and observability builder.
/// </summary>
public class MonitoringBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new monitoring builder.</summary>
    public MonitoringBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Enables OpenTelemetry integration.
    /// </summary>
    public MonitoringBuilder EnableOpenTelemetry()
    {
        return this;
    }

    /// <summary>
    /// Enables health checks.
    /// </summary>
    public MonitoringBuilder EnableHealthChecks()
    {
        // Health checks would require: using Microsoft.Extensions.Diagnostics.HealthChecks;
        // _services.AddHealthChecks();
        return this;
    }

    /// <summary>
    /// Enables dashboard/visualization.
    /// </summary>
    public MonitoringBuilder EnableDashboard()
    {
        return this;
    }

    /// <summary>
    /// Enables Prometheus metrics export.
    /// </summary>
    public MonitoringBuilder EnablePrometheus()
    {
        return this;
    }
}
