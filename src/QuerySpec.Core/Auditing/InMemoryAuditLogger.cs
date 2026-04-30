using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuerySpec.Core.Auditing;

/// <summary>
/// In-memory audit logger implementation for testing and demo purposes.
/// Thread-safe with <see cref="ReaderWriterLockSlim"/> for optimal concurrency. Implements
/// <see cref="IDisposable"/> so the kernel-backed lock is released when the DI container
/// disposes the singleton (or when callers <c>using</c> the type directly).
/// </summary>
public sealed class InMemoryAuditLogger : IAuditLogger, IDisposable
{
    private readonly List<AuditLogEntry> _logs = new();
    private readonly ReaderWriterLockSlim _lockSlim = new();
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    /// <summary>Initializes a new in-memory audit logger using the system clock.</summary>
    public InMemoryAuditLogger() : this(TimeProvider.System) { }

    /// <summary>
    /// Initializes a new in-memory audit logger with the supplied time provider.
    /// Inject a <c>FakeTimeProvider</c> in tests to control the cutoff used by
    /// <see cref="PurgeOldLogsAsync"/> without wall-clock waits.
    /// </summary>
    /// <param name="timeProvider">Time source consulted by <see cref="PurgeOldLogsAsync"/>. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    public InMemoryAuditLogger(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Releases the <see cref="ReaderWriterLockSlim"/> backing this logger. Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _lockSlim.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Logs a query audit entry, sealing it into the integrity chain by setting its
    /// <see cref="AuditLogEntry.PreviousHash"/> to the most recent entry's hash and
    /// computing its <see cref="AuditLogEntry.Hash"/>. Sealing happens under the write
    /// lock so concurrent appends cannot diverge.
    /// </summary>
    /// <param name="entry">Entry to seal and append. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when sealing <paramref name="entry"/> fails validation.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="entry"/> has already been sealed.</exception>
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
    /// <param name="change">Field-change record to validate; the in-memory implementation does not retain change records.</param>
    public Task LogChangeAsync(FieldChange change)
    {
        change.Validate();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves an audit entry by ID.
    /// </summary>
    /// <param name="id">Audit entry identifier.</param>
    /// <returns>The matching <see cref="AuditLogEntry"/>, or <c>null</c> when no entry has that id.</returns>
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
    /// Gets all audits for a specific request. Returns a materialised snapshot taken under
    /// the read lock so subsequent enumeration is safe even while writers are appending.
    /// </summary>
    /// <param name="requestId">Request correlation identifier.</param>
    /// <returns>A materialised snapshot of audit entries belonging to <paramref name="requestId"/>.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId)
    {
        _lockSlim.EnterReadLock();
        try
        {
            IEnumerable<AuditLogEntry> snapshot = _logs.Where(l => l.RequestId == requestId).ToList();
            return Task.FromResult(snapshot);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all audits for a specific user. Returns a materialised snapshot taken under the read
    /// lock so subsequent enumeration is safe even while writers are appending.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="userId"/>.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId)
        => GetAuditsByUserAsync(userId, since: null);

    /// <summary>
    /// Gets all audits for a specific user filtered by date. Returns a materialised snapshot
    /// taken under the read lock so subsequent enumeration is safe even while writers are
    /// appending.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="userId"/>.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId, DateTime? since)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var query = _logs.Where(l => l.UserId == userId);
            if (since.HasValue) query = query.Where(l => l.Timestamp >= since);
            IEnumerable<AuditLogEntry> snapshot = query.ToList();
            return Task.FromResult(snapshot);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all audits for a specific tenant. Returns a materialised snapshot taken under the
    /// read lock so subsequent enumeration is safe even while writers are appending.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="tenantId"/>.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId)
        => GetAuditsByTenantAsync(tenantId, since: null);

    /// <summary>
    /// Gets all audits for a specific tenant filtered by date. Returns a materialised snapshot
    /// taken under the read lock so subsequent enumeration is safe even while writers are
    /// appending.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="since">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns>A materialised snapshot of audit entries for <paramref name="tenantId"/>.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId, DateTime? since)
    {
        _lockSlim.EnterReadLock();
        try
        {
            var query = _logs.Where(l => l.TenantId == tenantId);
            if (since.HasValue) query = query.Where(l => l.Timestamp >= since);
            IEnumerable<AuditLogEntry> snapshot = query.ToList();
            return Task.FromResult(snapshot);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets compliance report for audits within date range. Returns a materialised snapshot
    /// taken under the read lock so subsequent enumeration is safe even while writers are
    /// appending.
    /// </summary>
    /// <param name="from">Inclusive lower bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <param name="to">Inclusive upper bound on <see cref="AuditLogEntry.Timestamp"/>.</param>
    /// <returns>A chronologically-ordered snapshot of audit entries within the requested window.</returns>
    public Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to)
    {
        _lockSlim.EnterReadLock();
        try
        {
            IEnumerable<AuditLogEntry> snapshot = _logs
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .OrderBy(l => l.Timestamp)
                .ToList();
            return Task.FromResult(snapshot);
        }
        finally
        {
            _lockSlim.ExitReadLock();
        }
    }

    /// <summary>
    /// Purges logs older than the specified timespan, but only when doing so does not break
    /// the integrity chain. Concretely, a partial purge that would orphan surviving entries
    /// (their <see cref="AuditLogEntry.PreviousHash"/> would reference a removed entry's
    /// <see cref="AuditLogEntry.Hash"/>) is refused with <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <remarks>
    /// Entries are appended chronologically, so old entries form a contiguous prefix of the log.
    /// Allowed cases:
    /// <list type="bullet">
    ///   <item><description>Nothing to purge — no-op.</description></item>
    ///   <item><description>All entries are older than the cutoff — log is cleared and the next
    ///   <see cref="LogQueryAsync"/> starts a fresh chain.</description></item>
    /// </list>
    /// Refused case: any partial prefix purge with surviving suffix entries — throws so the
    /// silent chain-break that this would cause never lands on disk or in a compliance export.
    /// Hosts that need time-windowed retention with chain integrity must use a logger backed
    /// by an append-only store with chain re-anchor support.
    /// </remarks>
    /// <param name="olderThan">Age threshold; entries with <see cref="AuditLogEntry.Timestamp"/> older than this are eligible for removal.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested purge would remove some, but not all, audit entries.
    /// </exception>
    public Task PurgeOldLogsAsync(TimeSpan olderThan)
    {
        _lockSlim.EnterWriteLock();
        try
        {
            if (_logs.Count == 0)
                return Task.CompletedTask;

            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.Subtract(olderThan);
            var firstSurvivor = _logs.FindIndex(l => l.Timestamp >= cutoff);

            if (firstSurvivor == -1)
            {
                // Every entry is older than the cutoff. Drop the whole log; chain is trivially
                // intact (next append starts with PreviousHash = null).
                _logs.Clear();
                return Task.CompletedTask;
            }

            if (firstSurvivor == 0)
            {
                // Nothing old enough to purge.
                return Task.CompletedTask;
            }

            // firstSurvivor > 0 — partial prefix purge would orphan _logs[firstSurvivor..]
            // from the chain. Refuse rather than silently corrupt the integrity chain.
            throw new InvalidOperationException(
                $"Refusing to purge {firstSurvivor} of {_logs.Count} audit entries: doing so would " +
                $"orphan {_logs.Count - firstSurvivor} survivor(s) from the integrity chain. " +
                "InMemoryAuditLogger only supports purging the entire log (e.g. when every entry is older than the cutoff) or no-op purges. " +
                "For time-windowed retention with chain integrity, use a logger backed by an append-only store with chain re-anchor support.");
        }
        finally
        {
            _lockSlim.ExitWriteLock();
        }
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
