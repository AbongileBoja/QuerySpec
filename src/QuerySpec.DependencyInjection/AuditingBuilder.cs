using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Auditing configuration builder.
/// </summary>
/// <remarks>
/// All configuration methods on this builder are unimplemented stubs and throw
/// <see cref="NotImplementedException"/> at configuration time. The methods are preserved on
/// the public surface so a future implementation can land without further API churn.
/// </remarks>
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

    /// <summary>Enables logging of all queries.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public AuditingBuilder LogAllQueries() =>
        throw new NotImplementedException("AuditingBuilder.LogAllQueries is not implemented.");

    /// <summary>Enables tracking of entity changes.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public AuditingBuilder TrackChanges() =>
        throw new NotImplementedException("AuditingBuilder.TrackChanges is not implemented.");

    /// <summary>Enables encryption of audit logs.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public AuditingBuilder EnableEncryption() =>
        throw new NotImplementedException("AuditingBuilder.EnableEncryption is not implemented.");

    /// <summary>Uses database-backed audit logger.</summary>
    /// <param name="connectionString">Connection string for the audit log database.</param>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public AuditingBuilder UseDatabase(string connectionString) =>
        throw new NotImplementedException("AuditingBuilder.UseDatabase is not implemented.");

    /// <summary>Sets retention period for audit logs.</summary>
    /// <param name="days">Retention period in days.</param>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Not implemented; throws NotImplementedException at configuration time.", error: false)]
    public AuditingBuilder RetentionDays(int days) =>
        throw new NotImplementedException("AuditingBuilder.RetentionDays is not implemented.");
}
