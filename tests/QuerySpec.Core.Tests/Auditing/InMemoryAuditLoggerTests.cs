using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for InMemoryAuditLogger.
/// </summary>
public class InMemoryAuditLoggerTests
{
    [Fact]
    public async Task LogQueryAsync_FirstEntry_HasNullPreviousHash()
    {
        var logger = new InMemoryAuditLogger();
        var entry = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" };

        await logger.LogQueryAsync(entry);

        Assert.Null(entry.PreviousHash);
        Assert.False(string.IsNullOrEmpty(entry.Hash));
    }

    [Fact]
    public async Task LogQueryAsync_SecondEntry_PreviousHashEqualsFirstEntryHash()
    {
        var logger = new InMemoryAuditLogger();
        var first = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" };
        var second = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Update" };

        await logger.LogQueryAsync(first);
        await logger.LogQueryAsync(second);

        Assert.Equal(first.Hash, second.PreviousHash);
    }

    [Fact]
    public async Task LogQueryAsync_HundredEntries_FormValidChain()
    {
        var logger = new InMemoryAuditLogger();
        var entries = new List<AuditLogEntry>();
        for (var i = 0; i < 100; i++)
        {
            var e = new AuditLogEntry { TenantId = "t", UserId = $"user{i}", Operation = "Query" };
            await logger.LogQueryAsync(e);
            entries.Add(e);
        }

        Assert.Equal(-1, AuditLogEntry.VerifyChain(entries));
    }

    [Fact]
    public async Task LogQueryAsync_NullEntry_Throws()
    {
        var logger = new InMemoryAuditLogger();

        await Assert.ThrowsAsync<ArgumentNullException>(() => logger.LogQueryAsync(null!));
    }

    [Fact]
    public async Task LogQueryAsync_AlreadySealedEntry_Throws()
    {
        var logger = new InMemoryAuditLogger();
        var entry = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" };
        entry.Seal(null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => logger.LogQueryAsync(entry));
    }

    /// <summary>Tests that LogQueryAsync stores the audit entry.</summary>
    [Fact]
    public async Task LogQueryAsync_Should_Store_Entry()
    {
        // Arrange
        var logger = new InMemoryAuditLogger();
        var entry = new AuditLogEntry
        {
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query"
        };

        // Act
        await logger.LogQueryAsync(entry);

        // Assert
        Assert.Equal(1, logger.Count);
    }

    /// <summary>Tests that GetAuditAsync returns the stored entry.</summary>
    [Fact]
    public async Task GetAuditAsync_Should_Return_Entry()
    {
        // Arrange
        var logger = new InMemoryAuditLogger();
        var entry = new AuditLogEntry
        {
            Id = "test-id",
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query"
        };
        await logger.LogQueryAsync(entry);

        // Act
        var result = await logger.GetAuditAsync("test-id");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-id", result.Id);
    }

    /// <summary>Tests that PurgeOldLogsAsync removes entries older than the specified age.</summary>
    [Fact]
    public async Task PurgeOldLogsAsync_Should_Remove_Old_Entries()
    {
        // Arrange
        var logger = new InMemoryAuditLogger();
        var oldEntry = new AuditLogEntry
        {
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query",
            Timestamp = DateTime.UtcNow.AddDays(-10)
        };
        await logger.LogQueryAsync(oldEntry);

        // Act
        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));

        // Assert
        Assert.Equal(0, logger.Count);
    }

    /// <summary>
    /// A partial purge (some old, some recent) would orphan the surviving entries from the
    /// integrity chain. The logger must refuse rather than silently corrupt the chain.
    /// </summary>
    [Fact]
    public async Task PurgeOldLogsAsync_PartialPurge_Throws_AndLeavesLogIntact()
    {
        var logger = new InMemoryAuditLogger();
        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = DateTime.UtcNow.AddDays(-10),
        };
        var fresh = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = DateTime.UtcNow,
        };
        await logger.LogQueryAsync(old);
        await logger.LogQueryAsync(fresh);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => logger.PurgeOldLogsAsync(TimeSpan.FromDays(1)));

        Assert.Equal(2, logger.Count);
    }

    /// <summary>
    /// Full purge (every entry older than cutoff) is allowed: the log is cleared and the next
    /// append starts a fresh chain that verifies cleanly.
    /// </summary>
    [Fact]
    public async Task PurgeOldLogsAsync_FullPurge_AllowsFreshChainOnNextAppend()
    {
        var logger = new InMemoryAuditLogger();
        var oldA = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = DateTime.UtcNow.AddDays(-10),
        };
        var oldB = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Update",
            Timestamp = DateTime.UtcNow.AddDays(-9),
        };
        await logger.LogQueryAsync(oldA);
        await logger.LogQueryAsync(oldB);

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));

        Assert.Equal(0, logger.Count);

        var next = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" };
        await logger.LogQueryAsync(next);
        Assert.Null(next.PreviousHash);
        Assert.Equal(-1, AuditLogEntry.VerifyChain(new List<AuditLogEntry> { next }));
    }

    /// <summary>No-op purge (nothing old enough) leaves the chain intact and verifying.</summary>
    [Fact]
    public async Task PurgeOldLogsAsync_NoEntriesOldEnough_LeavesChainIntact()
    {
        var logger = new InMemoryAuditLogger();
        var entries = new List<AuditLogEntry>();
        for (var i = 0; i < 5; i++)
        {
            var e = new AuditLogEntry { TenantId = "t", UserId = $"u{i}", Operation = "Query" };
            await logger.LogQueryAsync(e);
            entries.Add(e);
        }

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(30));

        Assert.Equal(5, logger.Count);
        Assert.Equal(-1, AuditLogEntry.VerifyChain(entries));
    }

    /// <summary>Purge on an empty log is a no-op.</summary>
    [Fact]
    public async Task PurgeOldLogsAsync_EmptyLog_NoOp()
    {
        var logger = new InMemoryAuditLogger();

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));

        Assert.Equal(0, logger.Count);
    }

    /// <summary>
    /// Read methods must return a materialised snapshot taken under the read lock. Iterating
    /// the result after subsequent writes must not observe the new entries and must not throw
    /// the "Collection was modified" InvalidOperationException — both of which were possible
    /// when the methods returned a deferred LINQ enumerable over the live list.
    /// </summary>
    [Fact]
    public async Task GetAuditsByUserAsync_ReturnsSnapshot_NotAffectedBySubsequentWrites()
    {
        var logger = new InMemoryAuditLogger();
        for (var i = 0; i < 5; i++)
        {
            await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" });
        }

        var snapshot = await logger.GetAuditsByUserAsync("u");

        // Append more entries after the read returned. A deferred enumerable would observe these.
        for (var i = 0; i < 5; i++)
        {
            await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" });
        }

        Assert.Equal(5, snapshot.Count());
        Assert.Equal(10, logger.Count);
    }

    /// <summary>Snapshot semantics must hold for the compliance-report range query as well.</summary>
    [Fact]
    public async Task GetComplianceReportAsync_ReturnsSnapshot_NotAffectedBySubsequentWrites()
    {
        var logger = new InMemoryAuditLogger();
        var windowStart = DateTime.UtcNow.AddMinutes(-10);
        var windowEnd = DateTime.UtcNow.AddMinutes(10);
        for (var i = 0; i < 3; i++)
        {
            await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" });
        }

        var snapshot = await logger.GetComplianceReportAsync(windowStart, windowEnd);

        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" });

        Assert.Equal(3, snapshot.Count());
    }
}
