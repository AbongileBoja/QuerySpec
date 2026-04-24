using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QuerySpec.Core.Auditing;

namespace QuerySpec.Core.Tests.Auditing;

/// <summary>
/// Unit tests for EntityAuditTrail.
/// </summary>
public class EntityAuditTrailTests
{
    /// <summary>Tests that GetChangesSince returns changes after the specified date.</summary>
    [Fact]
    public void GetChangesSince_Should_Return_Recent_Changes()
    {
        // Arrange
        var trail = new EntityAuditTrail
        {
            EntityId = "1",
            EntityType = "User",
            Changes = new List<FieldChange>
            {
                new FieldChange { FieldName = "Name", ChangedAt = DateTime.UtcNow.AddMinutes(-10) },
                new FieldChange { FieldName = "Email", ChangedAt = DateTime.UtcNow.AddMinutes(-1) }
            }
        };

        // Act
        var changes = trail.GetChangesSince(DateTime.UtcNow.AddMinutes(-5));

        // Assert
        Assert.Single(changes);
        Assert.Equal("Email", changes.First().FieldName);
    }

    /// <summary>Tests that GetChangesByUser filters changes by user.</summary>
    [Fact]
    public void GetChangesByUser_Should_Filter_By_User()
    {
        // Arrange
        var trail = new EntityAuditTrail
        {
            EntityId = "1",
            EntityType = "User",
            Changes = new List<FieldChange>
            {
                new FieldChange { FieldName = "Name", ChangedBy = "user1" },
                new FieldChange { FieldName = "Email", ChangedBy = "user2" }
            }
        };

        // Act
        var changes = trail.GetChangesByUser("user1");

        // Assert
        Assert.Single(changes);
        Assert.Equal("user1", changes.First().ChangedBy);
    }
}
