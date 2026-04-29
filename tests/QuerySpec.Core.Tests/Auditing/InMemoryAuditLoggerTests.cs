using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
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
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);
        var oldEntry = new AuditLogEntry
        {
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await logger.LogQueryAsync(oldEntry);

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));

        Assert.Equal(0, logger.Count);
    }

    /// <summary>
    /// A partial purge (some old, some recent) would orphan the surviving entries from the
    /// integrity chain. The logger must refuse rather than silently corrupt the chain.
    /// </summary>
    [Fact]
    public async Task PurgeOldLogsAsync_PartialPurge_Throws_AndLeavesLogIntact()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);
        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var fresh = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
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
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);
        var oldA = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Query",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var oldB = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Update",
            Timestamp = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
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
        var recent = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var logger = new InMemoryAuditLogger();
        var entries = new List<AuditLogEntry>();
        for (var i = 0; i < 5; i++)
        {
            var e = new AuditLogEntry { TenantId = "t", UserId = $"u{i}", Operation = "Query", Timestamp = recent };
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
    /// The CancellationToken-accepting overloads added in 2.1 are reachable through the
    /// IAuditLogger interface and behave identically to the legacy overloads when the token
    /// is not cancelled. Default interface methods are the bridge for binary compat.
    /// </summary>
    [Fact]
    public async Task IAuditLogger_CancellationOverloads_AreReachable()
    {
        IAuditLogger logger = new InMemoryAuditLogger();
        var entry = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" };

        await logger.LogQueryAsync(entry, System.Threading.CancellationToken.None);
        var fetched = await logger.GetAuditAsync(entry.Id, System.Threading.CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(entry.Id, fetched.Id);
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

    /// <summary>
    /// Dispose releases the <see cref="ReaderWriterLockSlim"/>. Calling Dispose twice must
    /// not throw — IDisposable contract requires idempotency.
    /// </summary>
    [Fact]
    public void Dispose_Idempotent_DoesNotThrow()
    {
        var logger = new InMemoryAuditLogger();
        logger.Dispose();
        logger.Dispose();
    }

    /// <summary>
    /// After disposal, the underlying <see cref="ReaderWriterLockSlim"/> is released. Any
    /// subsequent attempt to enter the lock surfaces as <see cref="ObjectDisposedException"/>
    /// from the lock primitive itself.
    /// </summary>
    [Fact]
    public async Task LogQueryAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var logger = new InMemoryAuditLogger();
        logger.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query" }));
    }

    /// <summary>Snapshot semantics must hold for the compliance-report range query as well.</summary>
    [Fact]
    public async Task GetComplianceReportAsync_ReturnsSnapshot_NotAffectedBySubsequentWrites()
    {
        var logger = new InMemoryAuditLogger();
        var entryTs = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var windowStart = entryTs.AddMinutes(-10);
        var windowEnd = entryTs.AddMinutes(10);
        for (var i = 0; i < 3; i++)
        {
            await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query", Timestamp = entryTs });
        }

        var snapshot = await logger.GetComplianceReportAsync(windowStart, windowEnd);

        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Query", Timestamp = entryTs });

        Assert.Equal(3, snapshot.Count());
    }

    [Fact]
    public async Task GetAuditsByRequestAsync_ReturnsOnlyMatchingRequestId()
    {
        var logger = new InMemoryAuditLogger();
        var ts = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", RequestId = "req-A", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", RequestId = "req-B", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", RequestId = "req-A", Operation = "Update", Timestamp = ts });

        var results = (await logger.GetAuditsByRequestAsync("req-A")).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("req-A", e.RequestId));
    }

    [Fact]
    public async Task GetAuditsByUserAsync_NoSince_ReturnsAllForUser()
    {
        var logger = new InMemoryAuditLogger();
        var ts = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "bob", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Update", Timestamp = ts });

        var results = (await logger.GetAuditsByUserAsync("alice")).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("alice", e.UserId));
    }

    [Fact]
    public async Task GetAuditsByUserAsync_WithSince_FiltersOnTimestampBoundary()
    {
        var logger = new InMemoryAuditLogger();
        var boundary = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var before = boundary.AddSeconds(-1);
        var at = boundary;
        var after = boundary.AddSeconds(1);

        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "A", Timestamp = before });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "B", Timestamp = at });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "C", Timestamp = after });

        var results = (await logger.GetAuditsByUserAsync("u", since: boundary)).ToList();

        // since is inclusive (implementation uses >=)
        Assert.Equal(2, results.Count);
        Assert.Contains(results, e => e.Timestamp == at);
        Assert.Contains(results, e => e.Timestamp == after);
        Assert.DoesNotContain(results, e => e.Timestamp == before);
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_NoSince_ReturnsAllForTenant()
    {
        var logger = new InMemoryAuditLogger();
        var ts = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "tenant-X", UserId = "u", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "tenant-Y", UserId = "u", Operation = "Query", Timestamp = ts });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "tenant-X", UserId = "u", Operation = "Update", Timestamp = ts });

        var results = (await logger.GetAuditsByTenantAsync("tenant-X")).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("tenant-X", e.TenantId));
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_WithSince_FiltersOnTimestampBoundary()
    {
        var logger = new InMemoryAuditLogger();
        var boundary = new DateTime(2025, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        var before = boundary.AddSeconds(-1);
        var at = boundary;
        var after = boundary.AddSeconds(1);

        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "A", Timestamp = before });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "B", Timestamp = at });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "C", Timestamp = after });

        var results = (await logger.GetAuditsByTenantAsync("t", since: boundary)).ToList();

        // since is inclusive (implementation uses >=)
        Assert.Equal(2, results.Count);
        Assert.Contains(results, e => e.Timestamp == at);
        Assert.Contains(results, e => e.Timestamp == after);
        Assert.DoesNotContain(results, e => e.Timestamp == before);
    }

    [Fact]
    public async Task GetComplianceReportAsync_WithinRange_ReturnsAscendingOrder()
    {
        var logger = new InMemoryAuditLogger();
        var base1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = base1.AddHours(3);
        var t2 = base1.AddHours(1);
        var t3 = base1.AddHours(2);
        var outside = base1.AddHours(5);

        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "A", Timestamp = t1 });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "B", Timestamp = t2 });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "C", Timestamp = t3 });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "D", Timestamp = outside });

        var from = base1;
        var to = base1.AddHours(4);
        var results = (await logger.GetComplianceReportAsync(from, to)).ToList();

        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, e => e.Timestamp == outside);
        Assert.Equal(t2, results[0].Timestamp);
        Assert.Equal(t3, results[1].Timestamp);
        Assert.Equal(t1, results[2].Timestamp);
    }

    [Fact]
    public async Task LogChangeAsync_InvalidFieldChange_Throws()
    {
        var logger = new InMemoryAuditLogger();
        var invalidChange = new FieldChange
        {
            FieldName = "",
            ChangedBy = "user1"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => logger.LogChangeAsync(invalidChange));
    }
}
