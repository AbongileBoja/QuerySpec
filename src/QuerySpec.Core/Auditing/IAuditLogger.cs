using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// Defines contracts for audit operations with segregated interfaces.
/// </summary>
public interface IAuditLogger : IAuditWriter, IAuditReader, IAuditMaintenance { }

/// <summary>
/// Handles writing audit entries.
/// </summary>
public interface IAuditWriter
{
    /// <summary>Logs a query audit entry.</summary>
    Task LogQueryAsync(AuditLogEntry entry);
    /// <summary>Logs a field change audit entry.</summary>
    Task LogChangeAsync(FieldChange change);
}

/// <summary>
/// Handles reading audit entries.
/// </summary>
public interface IAuditReader
{
    /// <summary>Retrieves an audit entry by ID.</summary>
    Task<AuditLogEntry?> GetAuditAsync(string id);
    /// <summary>Gets all audits for a specific request.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId);
    /// <summary>Gets all audits for a specific user, optionally filtered by date.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since = null);
    /// <summary>Gets all audits for a specific tenant, optionally filtered by date.</summary>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since = null);
    /// <summary>Gets compliance report for audits within date range.</summary>
    Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to);
}

/// <summary>
/// Handles maintenance of audit logs.
/// </summary>
public interface IAuditMaintenance
{
    /// <summary>Purges logs older than specified timespan.</summary>
    Task PurgeOldLogsAsync(TimeSpan olderThan);
}
