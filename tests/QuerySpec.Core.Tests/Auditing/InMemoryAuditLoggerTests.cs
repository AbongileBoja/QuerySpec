using System;
using System.Threading.Tasks;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for InMemoryAuditLogger.
/// </summary>
public class InMemoryAuditLoggerTests
{
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
}
