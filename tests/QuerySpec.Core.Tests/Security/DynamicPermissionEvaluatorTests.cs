using System.Collections.Generic;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Unit tests for DynamicPermissionEvaluator.
/// </summary>
public class DynamicPermissionEvaluatorTests
{
    /// <summary>Tests that HasPermission allows access when permission is granted.</summary>
    [Fact]
    public void HasPermission_Should_Allow_When_Permission_Granted()
    {
        // Arrange
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", DynamicPermissionEvaluator.PermissionType.Read, ctx => true);
        var context = new DynamicPermissionEvaluator.DynamicContext
        {
            UserId = "user1",
            Roles = new List<string> { "Admin" },
            ResourceType = "User",
            FieldName = "Email"
        };

        // Act
        var result = evaluator.HasPermission(context, DynamicPermissionEvaluator.PermissionType.Read);

        // Assert
        Assert.True(result);
    }

    /// <summary>Tests that HasPermission denies access when role is missing.</summary>
    [Fact]
    public void HasPermission_Should_Deny_When_Role_Missing()
    {
        // Arrange
        var evaluator = new DynamicPermissionEvaluator();
        var context = new DynamicPermissionEvaluator.DynamicContext
        {
            UserId = "user1",
            Roles = new List<string> { "User" },
            ResourceType = "User",
            FieldName = "Email"
        };

        // Act
        var result = evaluator.HasPermission(context, DynamicPermissionEvaluator.PermissionType.Write);

        // Assert
        Assert.False(result);
    }
}
