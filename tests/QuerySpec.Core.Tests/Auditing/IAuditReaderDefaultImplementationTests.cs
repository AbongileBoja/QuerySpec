using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuerySpec.Core.Auditing;
using Xunit;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Verifies that <see cref="IAuditReader"/> default implementations honour the documented contract
/// when an implementer overrides only the simplest overload — in particular, that the
/// <c>since</c> date filter is applied by the DIM rather than silently dropped.
/// </summary>
public class IAuditReaderDefaultImplementationTests
{
    [Fact]
    public async Task GetAuditsByUserAsync_WithSince_DimAppliesDateFilter()
    {
        IAuditReader reader = new MinimalReader(new[]
        {
            Entry("u1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry("u1", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry("u1", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)),
        });

        var since = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = (await reader.GetAuditsByUserAsync("u1", since)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.True(e.Timestamp >= since));
    }

    [Fact]
    public async Task GetAuditsByUserAsync_WithNullSince_DimReturnsAllEntries()
    {
        IAuditReader reader = new MinimalReader(new[]
        {
            Entry("u1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry("u1", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)),
        });

        var result = (await reader.GetAuditsByUserAsync("u1", since: null)).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_WithSince_DimAppliesDateFilter()
    {
        IAuditReader reader = new MinimalReader(new[]
        {
            EntryTenant("t1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            EntryTenant("t1", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            EntryTenant("t1", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)),
        });

        var since = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = (await reader.GetAuditsByTenantAsync("t1", since)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.True(e.Timestamp >= since));
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_WithNullSince_DimReturnsAllEntries()
    {
        IAuditReader reader = new MinimalReader(new[]
        {
            EntryTenant("t1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            EntryTenant("t1", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)),
        });

        var result = (await reader.GetAuditsByTenantAsync("t1", since: null)).ToList();

        Assert.Equal(2, result.Count);
    }

    private static AuditLogEntry Entry(string userId, DateTime timestamp)
        => new() { UserId = userId, TenantId = "t", Operation = "Query", Timestamp = timestamp };

    private static AuditLogEntry EntryTenant(string tenantId, DateTime timestamp)
        => new() { UserId = "u", TenantId = tenantId, Operation = "Query", Timestamp = timestamp };

    private sealed class MinimalReader : IAuditReader
    {
        private readonly IReadOnlyList<AuditLogEntry> _entries;

        public MinimalReader(IEnumerable<AuditLogEntry> entries)
        {
            _entries = entries.ToList();
        }

        public Task<AuditLogEntry?> GetAuditAsync(string id)
            => Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));

        public Task<IEnumerable<AuditLogEntry>> GetAuditsByRequestAsync(string requestId)
            => Task.FromResult<IEnumerable<AuditLogEntry>>(_entries.Where(e => e.RequestId == requestId).ToList());

        public Task<IEnumerable<AuditLogEntry>> GetAuditsByUserAsync(string userId)
            => Task.FromResult<IEnumerable<AuditLogEntry>>(_entries.Where(e => e.UserId == userId).ToList());

        public Task<IEnumerable<AuditLogEntry>> GetAuditsByTenantAsync(string tenantId)
            => Task.FromResult<IEnumerable<AuditLogEntry>>(_entries.Where(e => e.TenantId == tenantId).ToList());

        public Task<IEnumerable<AuditLogEntry>> GetComplianceReportAsync(DateTime from, DateTime to)
            => Task.FromResult<IEnumerable<AuditLogEntry>>(
                _entries.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToList());
    }
}
