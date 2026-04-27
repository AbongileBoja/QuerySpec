using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Auditing;
using QuerySpec.Core.Caching;

namespace QuerySpec.Core.Tests;

/// <summary>
/// Targeted mutation kill tests for surviving mutants identified in the full-library
/// Stryker run (baseline 74.36%). Each test is scoped to one or a small cluster of
/// related mutations so failures pinpoint the exact survivor.
/// </summary>
public class MutationKillerTests
{
    // ─── InMemoryAuditLogger mutations ───────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var logger = new InMemoryAuditLogger();
        logger.Dispose();
        logger.Dispose();
    }

    [Fact]
    public void Dispose_EmptyLogger_CountIsZeroBeforeDispose()
    {
        var logger = new InMemoryAuditLogger();
        Assert.Equal(0, logger.Count);
        logger.Dispose();
    }

    [Fact]
    public async Task LogQueryAsync_DisposedLogger_DoesNotThrowBeforeDispose()
    {
        var logger = new InMemoryAuditLogger();
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        await logger.LogQueryAsync(e);
        Assert.Equal(1, logger.Count);
        logger.Dispose();
    }

    [Fact]
    public async Task GetAuditAsync_ByExistingId_ReturnsEntry()
    {
        var logger = new InMemoryAuditLogger();
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        await logger.LogQueryAsync(e);
        var result = await logger.GetAuditAsync(e.Id);
        Assert.NotNull(result);
        Assert.Equal(e.Id, result!.Id);
    }

    [Fact]
    public async Task GetAuditAsync_ByMissingId_ReturnsNull()
    {
        var logger = new InMemoryAuditLogger();
        var result = await logger.GetAuditAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuditsByRequestAsync_ReturnsOnlyMatchingRequest()
    {
        var logger = new InMemoryAuditLogger();
        var e1 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", RequestId = "req-1" };
        var e2 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", RequestId = "req-2" };
        await logger.LogQueryAsync(e1);
        await logger.LogQueryAsync(e2);

        var results = (await logger.GetAuditsByRequestAsync("req-1")).ToList();
        Assert.Single(results);
        Assert.Equal("req-1", results[0].RequestId);
    }

    [Fact]
    public async Task GetAuditsByUserAsync_NoSince_ReturnsAllForUser()
    {
        var logger = new InMemoryAuditLogger();
        var e1 = new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Q" };
        var e2 = new AuditLogEntry { TenantId = "t", UserId = "bob", Operation = "Q" };
        var e3 = new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Q" };
        await logger.LogQueryAsync(e1);
        await logger.LogQueryAsync(e2);
        await logger.LogQueryAsync(e3);

        var results = (await logger.GetAuditsByUserAsync("alice")).ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("alice", r.UserId));
    }

    [Fact]
    public async Task GetAuditsByUserAsync_WithSince_ExcludesOlderEntries()
    {
        var logger = new InMemoryAuditLogger();
        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "alice",
            Operation = "Q",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var recent = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "alice",
            Operation = "Q",
            Timestamp = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await logger.LogQueryAsync(old);
        await logger.LogQueryAsync(recent);

        var cutoff = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = (await logger.GetAuditsByUserAsync("alice", since: cutoff)).ToList();
        Assert.Single(results);
        Assert.Equal(recent.Timestamp, results[0].Timestamp);
    }

    [Fact]
    public async Task GetAuditsByUserAsync_SinceIsInclusive_IncludesBoundaryEntry()
    {
        var logger = new InMemoryAuditLogger();
        var boundary = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var e = new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Q", Timestamp = boundary };
        await logger.LogQueryAsync(e);

        var results = (await logger.GetAuditsByUserAsync("alice", since: boundary)).ToList();
        Assert.Single(results);
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_NoSince_ReturnsAllForTenant()
    {
        var logger = new InMemoryAuditLogger();
        var e1 = new AuditLogEntry { TenantId = "tenant-a", UserId = "u1", Operation = "Q" };
        var e2 = new AuditLogEntry { TenantId = "tenant-b", UserId = "u2", Operation = "Q" };
        await logger.LogQueryAsync(e1);
        await logger.LogQueryAsync(e2);

        var results = (await logger.GetAuditsByTenantAsync("tenant-a")).ToList();
        Assert.Single(results);
        Assert.Equal("tenant-a", results[0].TenantId);
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_WithSince_FiltersCorrectly()
    {
        var logger = new InMemoryAuditLogger();
        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var recent = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await logger.LogQueryAsync(old);
        await logger.LogQueryAsync(recent);

        var cutoff = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = (await logger.GetAuditsByTenantAsync("t", since: cutoff)).ToList();
        Assert.Single(results);
        Assert.Equal(recent.Timestamp, results[0].Timestamp);
    }

    [Fact]
    public async Task GetAuditsByTenantAsync_SinceIsInclusive_IncludesBoundaryEntry()
    {
        var logger = new InMemoryAuditLogger();
        var boundary = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = boundary };
        await logger.LogQueryAsync(e);

        var results = (await logger.GetAuditsByTenantAsync("t", since: boundary)).ToList();
        Assert.Single(results);
    }

    [Fact]
    public async Task GetComplianceReportAsync_FiltersAndOrders()
    {
        var logger = new InMemoryAuditLogger();
        var t1 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var e1 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = t2 };
        var e2 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = t1 };
        var e3 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = t3 };
        await logger.LogQueryAsync(e1);
        await logger.LogQueryAsync(e2);
        await logger.LogQueryAsync(e3);

        var from = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var results = (await logger.GetComplianceReportAsync(from, to)).ToList();

        Assert.Single(results);
        Assert.Equal(t2, results[0].Timestamp);
    }

    [Fact]
    public async Task GetComplianceReportAsync_BoundariesAreInclusive()
    {
        var logger = new InMemoryAuditLogger();
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var eFrom = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = from };
        var eTo = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = to };
        await logger.LogQueryAsync(eFrom);
        await logger.LogQueryAsync(eTo);

        var results = (await logger.GetComplianceReportAsync(from, to)).ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetComplianceReportAsync_OutsideRange_ExcludesEntries()
    {
        var logger = new InMemoryAuditLogger();
        var outside = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = outside };
        await logger.LogQueryAsync(e);

        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var results = (await logger.GetComplianceReportAsync(from, to)).ToList();
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetComplianceReportAsync_ResultsAreOrdered_ByTimestampAscending()
    {
        var logger = new InMemoryAuditLogger();
        var t1 = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = t1 });
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q", Timestamp = t2 });

        var results = (await logger.GetComplianceReportAsync(t2, t1)).ToList();
        Assert.Equal(2, results.Count);
        Assert.True(results[0].Timestamp <= results[1].Timestamp);
    }

    [Fact]
    public async Task PurgeOldLogsAsync_EmptyLog_IsNoop()
    {
        var logger = new InMemoryAuditLogger();
        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PurgeOldLogsAsync_AllEntriesOld_ClearsLog()
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);

        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await logger.LogQueryAsync(old);

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(30));
        Assert.Equal(0, logger.Count);
    }

    [Fact]
    public async Task PurgeOldLogsAsync_NoEntriesOld_IsNoop()
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);

        var recent = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 5, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        await logger.LogQueryAsync(recent);

        await logger.PurgeOldLogsAsync(TimeSpan.FromDays(1));
        Assert.Equal(1, logger.Count);
    }

    [Fact]
    public async Task PurgeOldLogsAsync_PartialPurge_ThrowsInvalidOperation()
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var logger = new InMemoryAuditLogger(clock);

        var old = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var recent = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "u",
            Operation = "Q",
            Timestamp = new DateTime(2025, 5, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        await logger.LogQueryAsync(old);
        await logger.LogQueryAsync(recent);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            logger.PurgeOldLogsAsync(TimeSpan.FromDays(30)));
    }

    [Fact]
    public async Task Count_ReturnsCorrectCount_AfterLogging()
    {
        var logger = new InMemoryAuditLogger();
        Assert.Equal(0, logger.Count);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" });
        Assert.Equal(1, logger.Count);
        await logger.LogQueryAsync(new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" });
        Assert.Equal(2, logger.Count);
    }

    // ─── AuditLogEntry mutations ──────────────────────────────────────────────

    [Fact]
    public void AuditLogEntry_DefaultId_IsNonEmpty()
    {
        var e = new AuditLogEntry();
        Assert.False(string.IsNullOrEmpty(e.Id));
    }

    [Fact]
    public void Validate_EmptyTenantId_ThrowsWithCorrectParamName()
    {
        var e = new AuditLogEntry { UserId = "u", Operation = "Q" };
        var ex = Assert.Throws<ArgumentException>(() => e.Validate());
        Assert.Equal("TenantId", ex.ParamName);
    }

    [Fact]
    public void Validate_EmptyUserId_ThrowsWithCorrectParamName()
    {
        var e = new AuditLogEntry { TenantId = "t", Operation = "Q" };
        var ex = Assert.Throws<ArgumentException>(() => e.Validate());
        Assert.Equal("UserId", ex.ParamName);
    }

    [Fact]
    public void Validate_EmptyOperation_ThrowsWithCorrectParamName()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u" };
        var ex = Assert.Throws<ArgumentException>(() => e.Validate());
        Assert.Equal("Operation", ex.ParamName);
    }

    [Fact]
    public void Seal_SetsHashAndPreviousHash()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e.Seal("prev-hash");
        Assert.Equal("prev-hash", e.PreviousHash);
        Assert.False(string.IsNullOrEmpty(e.Hash));
    }

    [Fact]
    public void Seal_TwiceThrows_WithMessage()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e.Seal(null);
        var ex = Assert.Throws<InvalidOperationException>(() => e.Seal(null));
        Assert.Contains("already been sealed", ex.Message);
    }

    [Fact]
    public void VerifyIntegrity_UnsealedEntry_ReturnsFalse()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        Assert.False(e.VerifyIntegrity(null));
    }

    [Fact]
    public void VerifyIntegrity_SealedEntry_SamePrevious_ReturnsTrue()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e.Seal("abc");
        Assert.True(e.VerifyIntegrity("abc"));
    }

    [Fact]
    public void VerifyIntegrity_SealedEntry_DifferentPrevious_ReturnsFalse()
    {
        var e = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e.Seal("abc");
        Assert.False(e.VerifyIntegrity("wrong"));
    }

    [Fact]
    public void VerifyChain_EmptyList_ReturnsMinus1()
    {
        Assert.Equal(-1, AuditLogEntry.VerifyChain(new List<AuditLogEntry>()));
    }

    [Fact]
    public void VerifyChain_ValidChain_ReturnsMinus1()
    {
        var e1 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        var e2 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e1.Seal(null);
        e2.Seal(e1.Hash);
        Assert.Equal(-1, AuditLogEntry.VerifyChain(new[] { e1, e2 }));
    }

    [Fact]
    public void VerifyChain_BrokenChain_ReturnsIndexOfBrokenEntry()
    {
        var e1 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        var e2 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e1.Seal(null);
        e2.Seal("wrong-previous");
        Assert.Equal(1, AuditLogEntry.VerifyChain(new[] { e1, e2 }));
    }

    [Fact]
    public void VerifyChain_NullEntries_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AuditLogEntry.VerifyChain(null!));
    }

    [Fact]
    public void VerifyChain_FirstEntryWithNonNullPrevious_ReturnsZero()
    {
        var e1 = new AuditLogEntry { TenantId = "t", UserId = "u", Operation = "Q" };
        e1.Seal("should-be-null");
        Assert.Equal(0, AuditLogEntry.VerifyChain(new[] { e1 }));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(100, 0)]
    public void AuditLogEntry_RecordsAffected_StoredCorrectly(int count, int _)
    {
        var e = new AuditLogEntry { RecordsAffected = count };
        Assert.Equal(count, e.RecordsAffected);
    }

    [Fact]
    public void AuditLogEntry_WasEncrypted_DefaultFalse()
    {
        Assert.False(new AuditLogEntry().WasEncrypted);
    }

    [Fact]
    public void AuditLogEntry_WasMasked_DefaultFalse()
    {
        Assert.False(new AuditLogEntry().WasMasked);
    }

    [Fact]
    public void AuditLogEntry_Success_RoundTrips()
    {
        Assert.False(new AuditLogEntry { Success = false }.Success);
        Assert.True(new AuditLogEntry { Success = true }.Success);
    }

    // ─── FieldChange mutations ────────────────────────────────────────────────

    [Fact]
    public void FieldChange_Validate_EmptyFieldName_ThrowsWithCorrectParam()
    {
        var fc = new FieldChange { ChangedBy = "u" };
        var ex = Assert.Throws<ArgumentException>(() => fc.Validate());
        Assert.Equal("FieldName", ex.ParamName);
    }

    [Fact]
    public void FieldChange_Validate_EmptyChangedBy_ThrowsWithCorrectParam()
    {
        var fc = new FieldChange { FieldName = "Price" };
        var ex = Assert.Throws<ArgumentException>(() => fc.Validate());
        Assert.Equal("ChangedBy", ex.ParamName);
    }

    [Fact]
    public void FieldChange_Validate_ValidFields_DoesNotThrow()
    {
        var fc = new FieldChange { FieldName = "Price", ChangedBy = "alice" };
        fc.Validate();
    }

    [Fact]
    public void FieldChange_DefaultValues_AreCorrect()
    {
        var fc = new FieldChange();
        Assert.Equal(string.Empty, fc.FieldName);
        Assert.Equal(string.Empty, fc.ChangedBy);
        Assert.Equal(string.Empty, fc.ChangeReason);
        Assert.Null(fc.OldValue);
        Assert.Null(fc.NewValue);
    }

    // ─── AuditContext mutations ───────────────────────────────────────────────

    [Fact]
    public void AuditContext_NullAssignments_ThrowArgumentNullException()
    {
        var ctx = new AuditContext();
        Assert.Throws<ArgumentNullException>(() => ctx.RequestId = null!);
        Assert.Throws<ArgumentNullException>(() => ctx.TenantId = null!);
        Assert.Throws<ArgumentNullException>(() => ctx.UserId = null!);
        Assert.Throws<ArgumentNullException>(() => ctx.ResourceType = null!);
        Assert.Throws<ArgumentNullException>(() => ctx.Operation = null!);
        Assert.Throws<ArgumentNullException>(() => ctx.SensitiveFields = null!);
    }

    [Fact]
    public void AuditContext_DefaultsAreCorrect()
    {
        var ctx = new AuditContext();
        Assert.True(ctx.EnableAuditing);
        Assert.True(ctx.EnableFieldLevelTracking);
        Assert.False(ctx.EnableEncryption);
        Assert.False(ctx.EnableMasking);
        Assert.Equal("Query", ctx.Operation);
        Assert.NotNull(ctx.SensitiveFields);
    }

    // ─── EntityAuditTrail mutations ───────────────────────────────────────────

    [Fact]
    public void EntityAuditTrail_GetChangesSince_InclusiveBoundary()
    {
        var boundary = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var trail = new EntityAuditTrail();
        trail.Changes.Add(new FieldChange { FieldName = "F", ChangedBy = "u", ChangedAt = boundary });
        trail.Changes.Add(new FieldChange
        {
            FieldName = "F",
            ChangedBy = "u",
            ChangedAt = boundary.AddSeconds(-1)
        });

        var result = trail.GetChangesSince(boundary).ToList();
        Assert.Single(result);
        Assert.Equal(boundary, result[0].ChangedAt);
    }

    [Fact]
    public void EntityAuditTrail_GetChangesByUser_ReturnsOnlyMatchingUser()
    {
        var trail = new EntityAuditTrail();
        trail.Changes.Add(new FieldChange { FieldName = "F", ChangedBy = "alice" });
        trail.Changes.Add(new FieldChange { FieldName = "F", ChangedBy = "bob" });

        var result = trail.GetChangesByUser("alice").ToList();
        Assert.Single(result);
        Assert.Equal("alice", result[0].ChangedBy);
    }

    [Fact]
    public void EntityAuditTrail_GetChangesByUser_NoMatch_ReturnsEmpty()
    {
        var trail = new EntityAuditTrail();
        Assert.Empty(trail.GetChangesByUser("nobody"));
    }

    // ─── ComplianceExporter mutations ────────────────────────────────────────

    [Fact]
    public async Task ComplianceExporter_GetUserDataAsync_FiltersToTenant()
    {
        var logger = new InMemoryAuditLogger();
        var e1 = new AuditLogEntry { TenantId = "tenant-a", UserId = "alice", Operation = "Q" };
        var e2 = new AuditLogEntry { TenantId = "tenant-b", UserId = "alice", Operation = "Q" };
        await logger.LogQueryAsync(e1);
        await logger.LogQueryAsync(e2);

        var exporter = new ComplianceExporter(logger);
        var results = (await exporter.GetUserDataAsync("alice", "tenant-a")).ToList();
        Assert.Single(results);
        Assert.Equal("tenant-a", results[0].TenantId);
    }

    [Fact]
    public async Task ComplianceExporter_UserHasAccessedFieldAsync_TrueWhenFieldAccessed()
    {
        var logger = new InMemoryAuditLogger();
        var since = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var e = new AuditLogEntry
        {
            TenantId = "t",
            UserId = "alice",
            Operation = "Q",
            AccessedSensitiveFields = new List<string> { "SSN", "Email" }
        };
        await logger.LogQueryAsync(e);

        var exporter = new ComplianceExporter(logger);
        Assert.True(await exporter.UserHasAccessedFieldAsync("alice", "SSN", since));
        Assert.False(await exporter.UserHasAccessedFieldAsync("alice", "PhoneNumber", since));
    }

    [Fact]
    public async Task ComplianceExporter_GenerateGdprExportAsync_WritesJson()
    {
        var logger = new InMemoryAuditLogger();
        var e = new AuditLogEntry { TenantId = "t", UserId = "alice", Operation = "Q" };
        await logger.LogQueryAsync(e);

        var exporter = new ComplianceExporter(logger);
        using var ms = new MemoryStream();
        await exporter.GenerateGdprExportAsync("alice", "t", ms);
        ms.Position = 0;
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("alice", json);
    }

    [Fact]
    public void ComplianceExporter_NullReader_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ComplianceExporter(null!));
    }

    // ─── AdvancedFilterExpression mutations ──────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceField_ReturnsFieldRequiredError(string field)
    {
        var f = new AdvancedFilterExpression { Field = field };
        var errors = f.Validate().ToList();
        Assert.Contains("Field is required", errors);
    }

    [Fact]
    public void Validate_InvalidFieldNamePattern_ReturnsInvalidFieldNameError()
    {
        var f = new AdvancedFilterExpression { Field = "123invalid" };
        var errors = f.Validate().ToList();
        Assert.Single(errors);
        Assert.Contains("Invalid field name", errors[0]);
    }

    [Fact]
    public void Validate_ValidDottedField_NoErrors()
    {
        var f = new AdvancedFilterExpression { Field = "Customer.Name" };
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void Validate_TemporalStart_GreaterThan_TemporalEnd_ReturnsError()
    {
        var now = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var f = new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            TemporalStart = now.AddDays(1),
            TemporalEnd = now
        };
        var errors = f.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("TemporalStart must be before"));
    }

    [Fact]
    public void Validate_TemporalStartEqualsEnd_IsValid()
    {
        var now = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var f = new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            TemporalStart = now,
            TemporalEnd = now
        };
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void Validate_GeoRadiusNonPositive_WithGeoLocation_ReturnsError()
    {
        var f = new AdvancedFilterExpression
        {
            Field = "Location",
            GeoLocation = new GeoLocation(10m, 20m),
            GeoRadius = 0m
        };
        var errors = f.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("GeoRadius must be positive"));
    }

    [Fact]
    public void Validate_GeoRadiusPositive_NoGeoError()
    {
        var f = new AdvancedFilterExpression
        {
            Field = "Location",
            GeoLocation = new GeoLocation(10m, 20m),
            GeoRadius = 1.5m
        };
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void Validate_GeoRadiusNegative_ReturnsError()
    {
        var f = new AdvancedFilterExpression
        {
            Field = "Location",
            GeoLocation = new GeoLocation(10m, 20m),
            GeoRadius = -5m
        };
        Assert.Contains("GeoRadius must be positive", f.Validate().ToList().Select(e => e));
    }

    [Fact]
    public void Validate_NestedFilters_AreRecursivelyValidated()
    {
        var inner = new AdvancedFilterExpression { Field = "" };
        var outer = new AdvancedFilterExpression
        {
            Field = "Name",
            Filters = new List<AdvancedFilterExpression> { inner }
        };
        var errors = outer.Validate().ToList();
        Assert.Contains("Field is required", errors);
    }

    [Fact]
    public void Validate_ValidFilterWithNoNestedFilters_NoErrors()
    {
        var f = new AdvancedFilterExpression { Field = "Name", Filters = new List<AdvancedFilterExpression>() };
        Assert.Empty(f.Validate());
    }

#pragma warning disable CS0618
    [Fact]
    public void Validate_MaskResultTrue_ReturnsError()
    {
        var f = new AdvancedFilterExpression { Field = "Name", MaskResult = true };
        var errors = f.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("MaskResult"));
    }

    [Fact]
    public void Validate_EncryptValueTrue_ReturnsError()
    {
        var f = new AdvancedFilterExpression { Field = "Name", EncryptValue = true };
        var errors = f.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("EncryptValue"));
    }

    [Fact]
    public void Validate_MaskResultFalse_NoObsoleteErrors()
    {
        var f = new AdvancedFilterExpression { Field = "Name", MaskResult = false };
        Assert.Empty(f.Validate());
    }
#pragma warning restore CS0618

    [Fact]
    public void ComputeStableHash_SameFilter_ProducesSameHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Equal, Value = "test" };
        var f2 = new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Equal, Value = "test" };
        Assert.Equal(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentField_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Equal };
        var f2 = new AdvancedFilterExpression { Field = "Age", Operator = FilterOperator.Equal };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentCaseSensitive_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", CaseSensitive = false };
        var f2 = new AdvancedFilterExpression { Field = "Name", CaseSensitive = true };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentUseRegex_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", UseRegex = false };
        var f2 = new AdvancedFilterExpression { Field = "Name", UseRegex = true };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentIncludeDeleted_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", IncludeDeletedRecords = false };
        var f2 = new AdvancedFilterExpression { Field = "Name", IncludeDeletedRecords = true };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentLogic_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", Logic = LogicalOperator.And };
        var f2 = new AdvancedFilterExpression { Field = "Name", Logic = LogicalOperator.Or };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentTemporalStart_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", TemporalStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var f2 = new AdvancedFilterExpression { Field = "Name", TemporalStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentTemporalEnd_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", TemporalEnd = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var f2 = new AdvancedFilterExpression { Field = "Name", TemporalEnd = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_NullTemporalStart_VsSet_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name" };
        var f2 = new AdvancedFilterExpression { Field = "Name", TemporalStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentGeoLocation_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Loc", GeoLocation = new GeoLocation(10m, 20m) };
        var f2 = new AdvancedFilterExpression { Field = "Loc", GeoLocation = new GeoLocation(11m, 20m) };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentGeoRadius_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Loc", GeoRadius = 10m };
        var f2 = new AdvancedFilterExpression { Field = "Loc", GeoRadius = 20m };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentCustomOperatorName_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", CustomOperatorName = "op1" };
        var f2 = new AdvancedFilterExpression { Field = "Name", CustomOperatorName = "op2" };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_NullVsEmptyValue_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name", Value = null };
        var f2 = new AdvancedFilterExpression { Field = "Name", Value = "x" };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_WithNestedFilters_DifferentFromWithout()
    {
        var f1 = new AdvancedFilterExpression { Field = "Name" };
        var f2 = new AdvancedFilterExpression
        {
            Field = "Name",
            Filters = new List<AdvancedFilterExpression>
            {
                new AdvancedFilterExpression { Field = "Age" }
            }
        };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_DifferentFilterCount_ProducesDifferentHash()
    {
        var child = new AdvancedFilterExpression { Field = "Age" };
        var f1 = new AdvancedFilterExpression
        {
            Field = "Name",
            Filters = new List<AdvancedFilterExpression> { child }
        };
        var f2 = new AdvancedFilterExpression
        {
            Field = "Name",
            Filters = new List<AdvancedFilterExpression> { child, child }
        };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_EnumerableValue_DifferentFromScalar()
    {
        var f1 = new AdvancedFilterExpression { Field = "Tags", Value = new[] { "a", "b" } };
        var f2 = new AdvancedFilterExpression { Field = "Tags", Value = new[] { "a", "c" } };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    [Fact]
    public void ComputeStableHash_StringValue_VsIntValue_ProducesDifferentHash()
    {
        var f1 = new AdvancedFilterExpression { Field = "X", Value = "123" };
        var f2 = new AdvancedFilterExpression { Field = "X", Value = 123 };
        Assert.NotEqual(f1.ComputeStableHash(), f2.ComputeStableHash());
    }

    // ─── GeoLocation / Haversine mutations ───────────────────────────────────

    [Theory]
    [InlineData(51.5074, -0.1278, 48.8566, 2.3522, 340.0)]
    [InlineData(0.0, 0.0, 0.0, 0.0, 0.0)]
    public void GeoLocation_DistanceTo_ProducesExpectedKilometers(
        double lat1, double lon1, double lat2, double lon2, double expectedKm)
    {
        var a = new GeoLocation((decimal)lat1, (decimal)lon1);
        var b = new GeoLocation((decimal)lat2, (decimal)lon2);
        var dist = a.DistanceTo(b);
        if (expectedKm == 0.0)
            Assert.Equal(0.0, dist, precision: 5);
        else
            Assert.InRange(dist, expectedKm - 10, expectedKm + 10);
    }

    [Fact]
    public void GeoLocation_DistanceTo_IsNearlySymmetric()
    {
        var nyc = new GeoLocation(40.7128m, -74.0060m);
        var london = new GeoLocation(51.5074m, -0.1278m);
        var d1 = nyc.DistanceTo(london);
        var d2 = london.DistanceTo(nyc);
        Assert.InRange(Math.Abs(d1 - d2), 0, 0.01);
    }

    [Fact]
    public void GeoLocation_DefaultCtor_InitializesToZero()
    {
        var g = new GeoLocation();
        Assert.Equal(0m, g.Latitude);
        Assert.Equal(0m, g.Longitude);
    }

    [Fact]
    public void GeoLocation_ParameteredCtor_SetsCoordinates()
    {
        var g = new GeoLocation(10.5m, 20.3m);
        Assert.Equal(10.5m, g.Latitude);
        Assert.Equal(20.3m, g.Longitude);
    }

    [Fact]
    public void GeoLocation_DistanceTo_AntipodalPoints_IsApprox20015Km()
    {
        var north = new GeoLocation(90m, 0m);
        var south = new GeoLocation(-90m, 0m);
        var dist = north.DistanceTo(south);
        Assert.InRange(dist, 19000, 21000);
    }

    // ─── CacheKeyGenerator mutations ─────────────────────────────────────────

    [Fact]
    public void CacheKeyGenerator_GenerateKey_ContainsPrefixAndComponents()
    {
        var key = CacheKeyGenerator.GenerateKey("myns", "a", "b", "c");
        Assert.StartsWith("myns", key);
        Assert.Contains("a", key);
        Assert.Contains("b", key);
        Assert.Contains("c", key);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateKey_NullComponentsSkipped()
    {
        var keyWithNull = CacheKeyGenerator.GenerateKey("ns", null, "b");
        var keyWithout = CacheKeyGenerator.GenerateKey("ns", "b");
        Assert.Equal(keyWithout, keyWithNull);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateKey_EmptyStringComponentsSkipped()
    {
        var keyWithEmpty = CacheKeyGenerator.GenerateKey("ns", "", "b");
        var keyWithout = CacheKeyGenerator.GenerateKey("ns", "b");
        Assert.Equal(keyWithout, keyWithEmpty);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateKey_LongKey_OverMaxLength_ReturnsHashedKey()
    {
        var big = new string('z', 300);
        var key = CacheKeyGenerator.GenerateKey("prefix", big);
        Assert.True(key.Length <= 256);
        Assert.StartsWith("prefix:", key);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateKey_ShortKey_NotHashed_PreservesComponents()
    {
        var key = CacheKeyGenerator.GenerateKey("ns", "tenant1", "user1");
        Assert.Equal("ns:tenant1:user1", key);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateQueryCacheKey_ContainsPage()
    {
        var key0 = CacheKeyGenerator.GenerateQueryCacheKey("t", "u", "h1", "h2", 0);
        var key1 = CacheKeyGenerator.GenerateQueryCacheKey("t", "u", "h1", "h2", 1);
        Assert.NotEqual(key0, key1);
        Assert.Contains(":0", key0);
        Assert.Contains(":1", key1);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateQueryCacheKey_EmptyTenantAndUser_OmitsComponents()
    {
        var keyFull = CacheKeyGenerator.GenerateQueryCacheKey("t", "u", "hash", "sort", 5);
        var keyEmpty = CacheKeyGenerator.GenerateQueryCacheKey("", "", "hash", "sort", 5);
        Assert.NotEqual(keyFull, keyEmpty);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateSecurityPolicyCacheKey_StartsWith_policy()
    {
        var key = CacheKeyGenerator.GenerateSecurityPolicyCacheKey("Order");
        Assert.StartsWith("policy:", key);
        Assert.Contains("Order", key);
    }

    [Fact]
    public void CacheKeyGenerator_GeneratePermissionCacheKey_StartsWith_permission()
    {
        var key = CacheKeyGenerator.GeneratePermissionCacheKey("u1", "Order", "Price");
        Assert.StartsWith("permission:", key);
        Assert.Contains("u1", key);
        Assert.Contains("Order", key);
        Assert.Contains("Price", key);
    }

    [Fact]
    public void CacheKeyGenerator_GenerateKey_LongKeyOverHeapThreshold_StillHashes()
    {
        var huge = new string('x', 400);
        var key = CacheKeyGenerator.GenerateKey("pfx", huge);
        Assert.True(key.Length <= 256);
        Assert.StartsWith("pfx:", key);
    }

    // ─── CustomOperatorRegistry mutations ────────────────────────────────────

    [Fact]
    public void CustomOperatorRegistry_Register_FirstWins()
    {
        var registry = new CustomOperatorRegistry();
        var op1 = new TestCustomOperator("myOp", "First");
        var op2 = new TestCustomOperator("myOp", "Second");

        registry.Register(op1);
        registry.Register(op2);

        var result = registry.Get("myOp");
        Assert.NotNull(result);
        Assert.Equal("First", result!.Description);
    }

    [Fact]
    public void CustomOperatorRegistry_Get_MissingOp_ReturnsNull()
    {
        var registry = new CustomOperatorRegistry();
        Assert.Null(registry.Get("nonexistent"));
    }

    [Fact]
    public void CustomOperatorRegistry_GetAll_ReturnsAllRegistered()
    {
        var registry = new CustomOperatorRegistry();
        registry.Register(new TestCustomOperator("op1", "D1"));
        registry.Register(new TestCustomOperator("op2", "D2"));

        var all = registry.GetAll().ToList();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void CustomOperatorRegistry_GetAll_EmptyRegistry_ReturnsEmpty()
    {
        var registry = new CustomOperatorRegistry();
        Assert.Empty(registry.GetAll());
    }

    private sealed class TestCustomOperator : ICustomOperator
    {
        public string Name { get; }
        public string Description { get; }
        public Type[] SupportedTypes => new[] { typeof(string) };
        public TestCustomOperator(string name, string description) { Name = name; Description = description; }
        public object? Execute(object value, object filterValue) => null;
    }
}
