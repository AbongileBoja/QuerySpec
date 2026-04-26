using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Defines contracts for audit operations with segregated interfaces.
/// </summary>
/// <remarks>
/// Every async member has a paired <see cref="CancellationToken"/>-accepting overload added in 2.1.
/// The CT-less overloads are preserved for source compatibility and delegate to the CT overloads
/// with <see cref="CancellationToken.None"/>. Implementers that ship binary-against 2.0 continue to
/// satisfy the interface via the default implementations; new implementers should override the
/// CT overloads to honour cancellation.
/// </remarks>
public interface IAuditLogger : IAuditWriter, IAuditReader, IAuditMaintenance { }

/// <summary>
/// Handles writing audit entries.
/// </summary>
public interface IAuditWriter
{
    /// <summary>Logs a query audit entry.</summary>
    Task LogQueryAsync(AuditLogEntry entry);
    /// <summary>Logs a query audit entry with cancellation support.</summary>
    Task LogQueryAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        => LogQueryAsync(entry);

    /// <summary>Logs a field change audit entry.</summary>
    Task LogChangeAsync(FieldChange change);
    /// <summary>Logs a field change audit entry with cancellation support.</summary>
    Task LogChangeAsync(FieldChange change, CancellationToken cancellationToken)
        => LogChangeAsync(change);
}

/// <summary>
/// Handles reading audit entries.
/// </summary>
public interface IAuditReader
{
    /// <summary>Retrieves an audit entry by ID.</summary>
    Task<AuditLogEntry?> GetAuditAsync(string id);
    /// <summary>Retrieves an audit entry by ID with cancellation support.</summary>
    Task<AuditLogEntry?> GetAuditAsync(string id, CancellationToken cancellationToken)
        => GetAuditAsync(id);

    /// <summary>Gets all audits for a specific request.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId);
    /// <summary>Gets all audits for a specific request with cancellation support.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId, CancellationToken cancellationToken)
        => GetAuditsByRequestAsync(requestId);

    /// <summary>Gets all audits for a specific user, optionally filtered by date.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since = null);
    /// <summary>Gets all audits for a specific user, optionally filtered by date, with cancellation support.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since, CancellationToken cancellationToken)
        => GetAuditsByUserAsync(userId, since);

    /// <summary>Gets all audits for a specific tenant, optionally filtered by date.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since = null);
    /// <summary>Gets all audits for a specific tenant, optionally filtered by date, with cancellation support.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since, CancellationToken cancellationToken)
        => GetAuditsByTenantAsync(tenantId, since);

    /// <summary>Gets compliance report for audits within date range.</summary>
    Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to);
    /// <summary>Gets compliance report for audits within date range with cancellation support.</summary>
    Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        => GetComplianceReportAsync(from, to);
}

/// <summary>
/// Handles maintenance of audit logs.
/// </summary>
public interface IAuditMaintenance
{
    /// <summary>Purges logs older than specified timespan.</summary>
    Task PurgeOldLogsAsync(TimeSpan olderThan);
    /// <summary>Purges logs older than specified timespan with cancellation support.</summary>
    Task PurgeOldLogsAsync(TimeSpan olderThan, CancellationToken cancellationToken)
        => PurgeOldLogsAsync(olderThan);
}
