using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for AuditContext.
/// </summary>
public class AuditContextTests
{
    /// <summary>Tests that AuditContext generates a RequestId. I see you.</summary>
    [Fact]
    public void AuditContext_Should_Generate_RequestId()
    {
        // Arrange & Act
        var context = new AuditContext();

        // Assert
        Assert.False(string.IsNullOrEmpty(context.RequestId));
    }

    /// <summary>Tests that AuditContext has expected default values.</summary>
    [Fact]
    public void AuditContext_Should_Have_Default_Values()
    {
        // Arrange & Act
        var context = new AuditContext();

        // Assert
        Assert.True(context.EnableAuditing);
        Assert.True(context.EnableFieldLevelTracking);
        Assert.False(context.EnableEncryption);
        Assert.False(context.EnableMasking);
    }

    /// <summary>Tests that AuditContext allows property customization.</summary>
    [Fact]
    public void AuditContext_Should_Allow_Customization()
    {
        // Arrange
        var context = new AuditContext
        {
            TenantId = "tenant1",
            UserId = "user1",
            ResourceType = "User"
        };

        // Assert
        Assert.Equal("tenant1", context.TenantId);
        Assert.Equal("user1", context.UserId);
        Assert.Equal("User", context.ResourceType);
    }
}
