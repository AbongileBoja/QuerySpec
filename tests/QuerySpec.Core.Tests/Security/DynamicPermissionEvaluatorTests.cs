using System;
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
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", PermissionType.Read, ctx => true);
        var context = new DynamicContext
        {
            UserId = "user1",
            Roles = new List<string> { "Admin" },
            ResourceType = "User",
            FieldName = "Email"
        };

        var result = evaluator.HasPermission(context, PermissionType.Read);

        Assert.True(result);
    }

    /// <summary>Tests that HasPermission denies access when role is missing.</summary>
    [Fact]
    public void HasPermission_Should_Deny_When_Role_Missing()
    {
        var evaluator = new DynamicPermissionEvaluator();
        var context = new DynamicContext
        {
            UserId = "user1",
            Roles = new List<string> { "User" },
            ResourceType = "User",
            FieldName = "Email"
        };

        var result = evaluator.HasPermission(context, PermissionType.Write);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_NullContext_Throws()
    {
        var evaluator = new DynamicPermissionEvaluator();

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.HasPermission(null!, PermissionType.Read));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasPermission_NullOrEmptyUserId_Denies(string? userId)
    {
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", PermissionType.Read, ctx => true);
        var context = new DynamicContext
        {
            UserId = userId!,
            Roles = new List<string> { "Admin" }
        };

        var result = evaluator.HasPermission(context, PermissionType.Read);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_NullRoles_Denies()
    {
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", PermissionType.Read, ctx => true);
        var context = new DynamicContext
        {
            UserId = "user1",
            Roles = null!
        };

        var result = evaluator.HasPermission(context, PermissionType.Read);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_EmptyRoles_Denies()
    {
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", PermissionType.Read, ctx => true);
        var context = new DynamicContext
        {
            UserId = "user1",
            Roles = new List<string>()
        };

        var result = evaluator.HasPermission(context, PermissionType.Read);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_NullOrEmptyRoleEntry_IsSkipped()
    {
        var evaluator = new DynamicPermissionEvaluator();
        evaluator.RegisterPermission("Admin", PermissionType.Read, ctx => true);
        var context = new DynamicContext
        {
            UserId = "user1",
            Roles = new List<string> { null!, "", "   ", "Admin" }
        };

        var result = evaluator.HasPermission(context, PermissionType.Read);

        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterPermission_EmptyOrWhitespaceRole_Throws(string role)
    {
        var evaluator = new DynamicPermissionEvaluator();

        Assert.Throws<ArgumentException>(() =>
            evaluator.RegisterPermission(role, PermissionType.Read, ctx => true));
    }

    [Fact]
    public void RegisterPermission_NullRole_Throws()
    {
        var evaluator = new DynamicPermissionEvaluator();

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.RegisterPermission(null!, PermissionType.Read, ctx => true));
    }

    [Fact]
    public void RegisterPermission_NullEvaluator_Throws()
    {
        var evaluator = new DynamicPermissionEvaluator();

        Assert.Throws<ArgumentNullException>(() =>
            evaluator.RegisterPermission("Admin", PermissionType.Read, null!));
    }

    [Fact]
    public void DynamicContext_AccessTime_CanBeSetAndRead()
    {
        var ts = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var ctx = new DynamicContext { UserId = "u1", AccessTime = ts };
        Assert.Equal(ts, ctx.AccessTime);
    }
}
