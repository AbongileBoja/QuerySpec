using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Auditing configuration builder.
/// </summary>
public class AuditingBuilder
{
    /// <summary>
    /// The underlying <see cref="IServiceCollection"/> the builder writes to. Exposed so
    /// third-party packages can author <c>Use*</c> extension methods that compose with the
    /// fluent QuerySpec API. Matches the convention of <c>IHealthChecksBuilder.Services</c>.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new auditing builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public AuditingBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }
}
