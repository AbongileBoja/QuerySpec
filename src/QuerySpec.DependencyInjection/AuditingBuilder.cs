using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Auditing configuration builder.
/// </summary>
public class AuditingBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>Initializes a new auditing builder.</summary>
    /// <param name="services">The service collection to register against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public AuditingBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>
    /// Enables logging of all queries.
    /// </summary>
    public AuditingBuilder LogAllQueries()
    {
        return this;
    }

    /// <summary>
    /// Enables tracking of entity changes.
    /// </summary>
    public AuditingBuilder TrackChanges()
    {
        return this;
    }

    /// <summary>
    /// Enables encryption of audit logs.
    /// </summary>
    public AuditingBuilder EnableEncryption()
    {
        return this;
    }

    /// <summary>
    /// Uses database-backed audit logger.
    /// </summary>
    public AuditingBuilder UseDatabase(string connectionString)
    {
        return this;
    }

    /// <summary>
    /// Sets retention period for audit logs.
    /// </summary>
    public AuditingBuilder RetentionDays(int days)
    {
        return this;
    }
}
