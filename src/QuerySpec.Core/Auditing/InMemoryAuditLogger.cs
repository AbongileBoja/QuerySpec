using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// In-memory audit logger implementation for testing and demo purposes.
/// Thread-safe with ReaderWriterLockSlim for optimal concurrency.
/// </summary>
public class InMemoryAuditLogger : IAuditLogger
{
    private readonly List<AuditLogEntry> _logs = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();

    /// <summary>Initializes a new in-memory audit logger.</summary>
    public InMemoryAuditLogger() { }

    /// <summary>
    /// Logs a query audit entry, sealing it into the integrity chain by setting its
    /// <see cref="AuditLogEntry.PreviousHash"/> to the most recent entry's hash and
    /// computing its <see cref="AuditLogEntry.Hash"/>. Sealing happens under the write
    /// lock so concurrent appends cannot diverge.
    /// </summary>
    public Task LogQueryAsync(AuditLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _lockSlim.EnterWriteLock();
        try
        {
            var previousHash = _logs.Count > 0 ? _logs[^1].Hash : null;
            entry.Seal(previousHash);
            _logs.Add(entry);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a field change.
    /// </summary>
    public Task LogChangeAsync(FieldChange change)
    {
        change.Validate();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves an audit entry by ID.
    /// </summary>
    public Task<AuditLogEntry?> GetAuditAsync(string id)
    {
        _lockSlim.EnterReadLock();
        try
        {
            return Task.FromResult(_logs.FirstOrDefault(l => l.Id == id));
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all audits for a specific request.
    /// </summary>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId)
    {
        _lockSlim.EnterReadLock();
        try
        {
            return Task.FromResult(_logs.Where(l => l.RequestId == requestId).AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all audits for a specific user, optionally filtered by date.
    /// </summary>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since = null)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var result = _logs.Where(l => l.UserId == userId);
            if (since.HasValue) result = result.Where(l => l.Timestamp >= since);
            return Task.FromResult(result.AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all audits for a specific tenant, optionally filtered by date.
    /// </summary>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since = null)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var result = _logs.Where(l => l.TenantId == tenantId);
            if (since.HasValue) result = result.Where(l => l.Timestamp >= since);
            return Task.FromResult(result.AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets compliance report for audits within date range.
    /// </summary>
    public Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to)
    {
        _lockSlim.EnterReadLock();
        try
        {
            return Task.FromResult(_logs
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .OrderBy(l => l.Timestamp)
                .AsEnumerable());
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Purges logs older than specified timespan.
    /// </summary>
    public Task PurgeOldLogsAsync(TimeSpan olderThan)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(olderThan);
            _logs.RemoveAll(l => l.Timestamp < cutoff);
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <summary>Gets the current count of audit entries.</summary>
    public int Count
    {
        get
        {
            _lockSlim.EnterReadLock();
            try
            {
                return _logs.Count;
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }
    }
}
