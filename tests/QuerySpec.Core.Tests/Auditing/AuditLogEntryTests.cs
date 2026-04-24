using System;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for AuditLogEntry.
/// </summary>
public class AuditLogEntryTests
{
    /// <summary>Tests that AuditLogEntry generates a default Id.</summary>
    [Fact]
    public void AuditLogEntry_Should_Generate_Default_Id()
    {
        // Arrange & Act
        var entry = new AuditLogEntry();

        // Assert
        Assert.False(string.IsNullOrEmpty(entry.Id));
        Assert.NotNull(entry.Id);
    }

    /// <summary>Tests that ComputeHash sets the hash property.</summary>
    [Fact]
    public void ComputeHash_Should_Set_Hash()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query"
        };

        // Act
        entry.ComputeHash();

        // Assert
        Assert.False(string.IsNullOrEmpty(entry.Hash));
    }

    /// <summary>Tests that Validate throws when TenantId is empty.</summary>
    [Fact]
    public void Validate_Should_Throw_When_TenantId_Empty()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            TenantId = "",
            UserId = "user1",
            Operation = "Query"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => entry.Validate());
    }

    /// <summary>Tests that VerifyIntegrity returns true for matching hash.</summary>
    [Fact]
    public void VerifyIntegrity_Should_Return_True_For_Same_Hash()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            TenantId = "tenant1",
            UserId = "user1",
            Operation = "Query"
        };
        entry.ComputeHash();
        var originalHash = entry.Hash;

        // Act
        var result = entry.VerifyIntegrity((string?)null);

        // Assert
        Assert.True(result);
    }
}
