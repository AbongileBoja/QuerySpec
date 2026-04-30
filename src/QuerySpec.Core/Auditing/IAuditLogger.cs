using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <param name="entry">Entry to seal and append. Must not be null.</param>
    Task LogQueryAsync(AuditLogEntry entry);
    /// <summary>Logs a query audit entry with cancellation support.</summary>
    /// <param name="entry">Entry to seal and append. Must not be null.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    Task LogQueryAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        => LogQueryAsync(entry);

    /// <summary>Logs a field change audit entry.</summary>
    /// <param name="change">Field-change record to log.</param>
    Task LogChangeAsync(FieldChange change);
    /// <summary>Logs a field change audit entry with cancellation support.</summary>
    /// <param name="change">Field-change record to log.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    Task LogChangeAsync(FieldChange change, CancellationToken cancellationToken)
        => LogChangeAsync(change);
}

/// <summary>
/// Handles reading audit entries.
/// </summary>
public interface IAuditReader
{
    /// <summary>Retrieves an audit entry by ID.</summary>
    /// <param name="id">Audit entry identifier.</param>
    /// <returns>The matching <see cref="AuditLogEntry"/>, or <c>null</c> when no entry has that id.</returns>
    Task<AuditLogEntry?> GetAuditAsync(string id);
    /// <summary>Retrieves an audit entry by ID with cancellation support.</summary>
    /// <param name="id">Audit entry identifier.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>The matching <see cref="AuditLogEntry"/>, or <c>null</c> when no entry has that id.</returns>
    Task<AuditLogEntry?> GetAuditAsync(string id, CancellationToken cancellationToken)
        => GetAuditAsync(id);

    /// <summary>Gets all audits for a specific request.</summary>
    /// <param name="requestId">Request correlation identifier.</param>
    /// <returns>A materialised snapshot of audit entries belonging to <paramref name="requestId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId);
    /// <summary>Gets all audits for a specific request with cancellation support.</summary>
    /// <param name="requestId">Request correlation identifier.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A materialised snapshot of audit entries belonging to <paramref name="requestId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId, CancellationToken cancellationToken)
        => GetAuditsByRequestAsync(requestId);

    /// <summary>Gets all audits for a specific user.</summary>
    /// <param name="userId">User identifier.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="userId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId);
    /// <summary>Gets all audits for a specific user filtered by date.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>; pass <c>null</c> to skip filtering.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="userId"/>.</returns>
    /// <remarks>
    /// The default implementation calls <see cref="GetAuditsByUserAsync(string)"/> and post-filters by
    /// <paramref name="since"/>. Implementations that can push the predicate to the storage layer should
    /// override this overload for efficiency.
    /// </remarks>
    async Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since)
    {
        var all = await GetAuditsByUserAsync(userId).ConfigureAwait(false);
        return since.HasValue ? all.Where(e => e.Timestamp >= since.Value) : all;
    }
    /// <summary>Gets all audits for a specific user filtered by date, with cancellation support.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="userId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since, CancellationToken cancellationToken)
        => GetAuditsByUserAsync(userId, since);

    /// <summary>Gets all audits for a specific tenant.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="tenantId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId);
    /// <summary>Gets all audits for a specific tenant filtered by date.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>; pass <c>null</c> to skip filtering.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="tenantId"/>.</returns>
    /// <remarks>
    /// The default implementation calls <see cref="GetAuditsByTenantAsync(string)"/> and post-filters by
    /// <paramref name="since"/>. Implementations that can push the predicate to the storage layer should
    /// override this overload for efficiency.
    /// </remarks>
    async Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since)
    {
        var all = await GetAuditsByTenantAsync(tenantId).ConfigureAwait(false);
        return since.HasValue ? all.Where(e => e.Timestamp >= since.Value) : all;
    }
    /// <summary>Gets all audits for a specific tenant filtered by date, with cancellation support.</summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="tenantId"/>.</returns>
    Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since, CancellationToken cancellationToken)
        => GetAuditsByTenantAsync(tenantId, since);

    /// <summary>Gets compliance report for audits within date range.</summary>
    /// <param name="from">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="to">Inclusive upper bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns>A chronologically-ordered snapshot of audit entries within the requested window.</returns>
    Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to);
    /// <summary>Gets compliance report for audits within date range with cancellation support.</summary>
    /// <param name="from">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="to">Inclusive upper bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    /// <returns>A chronologically-ordered snapshot of audit entries within the requested window.</returns>
    Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        => GetComplianceReportAsync(from, to);
}

/// <summary>
/// Handles maintenance of audit logs.
/// </summary>
public interface IAuditMaintenance
{
    /// <summary>Purges logs older than specified timespan.</summary>
    /// <param name="olderThan">Age threshold; entries with <see cref="AuditLogEntry.Timestamp"/> older than this are eligible for removal.</param>
    Task PurgeOldLogsAsync(TimeSpan olderThan);
    /// <summary>Purges logs older than specified timespan with cancellation support.</summary>
    /// <param name="olderThan">Age threshold; entries with <see cref="AuditLogEntry.Timestamp"/> older than this are eligible for removal.</param>
    /// <param name="cancellationToken">Token observed by implementations that perform I/O.</param>
    Task PurgeOldLogsAsync(TimeSpan olderThan, CancellationToken cancellationToken)
        => PurgeOldLogsAsync(olderThan);
}
