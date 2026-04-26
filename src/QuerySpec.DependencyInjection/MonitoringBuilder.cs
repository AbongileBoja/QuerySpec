using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Monitoring and observability builder.
/// </summary>
public class MonitoringBuilder
{
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>Use*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new monitoring builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public MonitoringBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>Enables OpenTelemetry integration.</summary>
    /// <exception cref="NotImplementedException">Always thrown. OpenTelemetry integration is not implemented.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public MonitoringBuilder EnableOpenTelemetry() =>
        throw new NotImplementedException("MonitoringBuilder.EnableOpenTelemetry is not implemented.");

    /// <summary>Enables health checks.</summary>
    /// <exception cref="NotImplementedException">Always thrown. Health-checks integration is not implemented.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public MonitoringBuilder EnableHealthChecks() =>
        throw new NotImplementedException("MonitoringBuilder.EnableHealthChecks is not implemented.");
}
