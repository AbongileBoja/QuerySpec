using System;
using Microsoft.Extensions.DependencyInjection;

namespace QuerySpec.DependencyInjection;

/// <summary>
/// Auditing configuration builder.
/// </summary>
/// <remarks>
/// Every configuration method declared on this builder throws
/// <see cref="NotImplementedException"/> at configuration time and is scheduled for removal
/// in 3.0. No implementation is planned. Wire your own <c>IAuditLogger</c> via
/// <c>WithAuditing(a =&gt; a.UseLogger(myLogger))</c> instead. See
/// <see href="https://github.com/AbongileBoja/QuerySpec/issues/139">issue #139</see>.
/// </remarks>
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

    /// <summary>Enables logging of all queries.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Wire your own IAuditLogger via WithAuditing(a => a.UseLogger(myLogger)) or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public AuditingBuilder LogAllQueries() =>
        throw new NotImplementedException("AuditingBuilder.LogAllQueries is not implemented.");

    /// <summary>Enables tracking of entity changes.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Implement change tracking inside your own IAuditLogger or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public AuditingBuilder TrackChanges() =>
        throw new NotImplementedException("AuditingBuilder.TrackChanges is not implemented.");

    /// <summary>Enables encryption of audit logs.</summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Encrypt entries inside your own IAuditLogger using IEncryptionProvider, or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public AuditingBuilder EnableEncryption() =>
        throw new NotImplementedException("AuditingBuilder.EnableEncryption is not implemented.");

    /// <summary>Uses database-backed audit logger.</summary>
    /// <param name="connectionString">Connection string for the audit log database.</param>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Wire your own database-backed IAuditLogger via WithAuditing(a => a.UseLogger(myLogger)) or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public AuditingBuilder UseDatabase(string connectionString) =>
        throw new NotImplementedException("AuditingBuilder.UseDatabase is not implemented.");

    /// <summary>Sets retention period for audit logs.</summary>
    /// <param name="days">Retention period in days.</param>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    [Obsolete("Will be removed in 3.0; this method has never been implemented. Use IAuditMaintenance.PurgeOldLogsAsync on a schedule of your choosing instead, or remove the call site. Tracked in https://github.com/AbongileBoja/QuerySpec/issues/139.", error: true)]
    public AuditingBuilder RetentionDays(int days) =>
        throw new NotImplementedException("AuditingBuilder.RetentionDays is not implemented.");
}
