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
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Call Services.AddOpenTelemetry() directly on the builder's Services property and wire the SDK against your own IAuditLogger / ICacheProvider instances. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public MonitoringBuilder EnableOpenTelemetry() =>
        throw new NotImplementedException("MonitoringBuilder.EnableOpenTelemetry is not implemented.");

    /// <summary>Enables health checks.</summary>
    /// <exception cref="NotImplementedException">Always thrown. Health-checks integration is not implemented.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Call Services.AddHealthChecks() directly on the builder's Services property and register the checks against your own dependencies. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public MonitoringBuilder EnableHealthChecks() =>
        throw new NotImplementedException("MonitoringBuilder.EnableHealthChecks is not implemented.");
}
