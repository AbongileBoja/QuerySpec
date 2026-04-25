using System;
using System.Collections.Generic;
using Xunit;
using QuerySpec.Core.Security;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Unit tests for RowLevelSecurityEngine.
/// </summary>
public class RowLevelSecurityEngineTests
{
    /// <summary>
    /// With the default (Throw) behavior, requesting a filter for an unregistered resource
    /// type throws — this is the fail-closed default and matches issue #10.
    /// </summary>
    [Fact]
    public void GenerateFilter_NoPolicy_DefaultThrows_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        var context = new RLSContext { UserId = "user1" };

        Assert.Throws<InvalidOperationException>(() => engine.GenerateFilter("User", context));
    }

    /// <summary>
    /// When constructed with <see cref="RLSDefaultBehavior.DenyAll"/>, an unregistered resource
    /// returns the deny-all filter — fail closed without throwing.
    /// </summary>
    [Fact]
    public void GenerateFilter_NoPolicy_DefaultDenyAll_ReturnsDenyAllFilter()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.DenyAll);
        var context = new RLSContext { UserId = "user1" };

        var filter = engine.GenerateFilter("User", context);

        Assert.Same(RLSFilter.DenyAll, filter);
        Assert.Equal("1=0", filter.Sql);
    }

    /// <summary>
    /// When constructed with <see cref="RLSDefaultBehavior.AllowAll"/>, an unregistered resource
    /// returns the allow-all filter — the explicit opt-in for legitimate "no RLS" scenarios.
    /// </summary>
    [Fact]
    public void GenerateFilter_NoPolicy_DefaultAllowAll_ReturnsAllowAllFilter()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.AllowAll);
        var context = new RLSContext { UserId = "user1" };

        var filter = engine.GenerateFilter("User", context);

        Assert.Same(RLSFilter.AllowAll, filter);
        Assert.Equal("1=1", filter.Sql);
    }

    /// <summary>
    /// <see cref="RowLevelSecurityEngine.RegisterUnrestricted{T}(string)"/> registers an explicit
    /// allow-all policy for a single resource type without weakening the engine-wide default.
    /// </summary>
    [Fact]
    public void RegisterUnrestricted_ThenGenerateFilter_ReturnsAllowAll()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterUnrestricted<object>("User");

        var filter = engine.GenerateFilter("User", new RLSContext());

        Assert.Same(RLSFilter.AllowAll, filter);
    }

    /// <summary>Tests that department-based policy parameterizes the value.</summary>
    [Fact]
    public void Department_Policy_Parameterizes_Value()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateDepartmentBased("Department");
        policy.ResourceType = "User";
        engine.RegisterPolicy(policy);
        var context = new RLSContext { UserId = "user1", Department = "Sales" };

        var filter = engine.GenerateFilter("User", context);

        Assert.Equal("Department = @rls_dept", filter.Sql);
        Assert.Equal("Sales", filter.Parameters["rls_dept"]);
    }

    /// <summary>Tests that tenant policy parameterizes each allowed tenant.</summary>
    [Fact]
    public void Tenant_Policy_Parameterizes_Each_Value()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "User";
        engine.RegisterPolicy(policy);
        var context = new RLSContext
        {
            UserId = "user1",
            AllowedTenants = new List<string> { "tenant1", "tenant2" }
        };

        var filter = engine.GenerateFilter("User", context);

        Assert.Equal("TenantId IN (@rls_tenant_0,@rls_tenant_1)", filter.Sql);
        Assert.Equal("tenant1", filter.Parameters["rls_tenant_0"]);
        Assert.Equal("tenant2", filter.Parameters["rls_tenant_1"]);
    }

    /// <summary>Empty tenant allow-list fails closed.</summary>
    [Fact]
    public void Tenant_Policy_Empty_Allowlist_Denies_All()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "User";
        engine.RegisterPolicy(policy);
        var context = new RLSContext { UserId = "user1" };

        var filter = engine.GenerateFilter("User", context);

        Assert.Same(RLSFilter.DenyAll, filter);
    }

    /// <summary>Injection attempt via tenant value does not break the SQL.</summary>
    [Fact]
    public void Tenant_Policy_Injection_Attempt_Is_Parameterized()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = RowLevelSecurityEngine.CreateTenantBased("TenantId");
        policy.ResourceType = "User";
        engine.RegisterPolicy(policy);
        var malicious = "x') OR 1=1 --";
        var context = new RLSContext
        {
            AllowedTenants = new List<string> { malicious }
        };

        var filter = engine.GenerateFilter("User", context);

        Assert.Equal("TenantId IN (@rls_tenant_0)", filter.Sql);
        Assert.Equal(malicious, filter.Parameters["rls_tenant_0"]);
        Assert.DoesNotContain("OR 1=1", filter.Sql, StringComparison.Ordinal);
    }

    /// <summary>Invalid column names are rejected at factory time.</summary>
    [Fact]
    public void Factories_Reject_Invalid_Identifiers()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.CreateDepartmentBased("dept; DROP TABLE x"));
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.CreateTenantBased("1invalid"));
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.CreateOwnerBased(""));
    }

    /// <summary>EscapeSqlLiteral doubles embedded quotes.</summary>
    [Fact]
    public void EscapeSqlLiteral_Doubles_Quotes()
    {
        Assert.Equal("'O''Brien'", RowLevelSecurityEngine.EscapeSqlLiteral("O'Brien"));
        Assert.Equal("NULL", RowLevelSecurityEngine.EscapeSqlLiteral(null));
    }

    /// <summary>Null characters are rejected in literals.</summary>
    [Fact]
    public void EscapeSqlLiteral_Rejects_Null_Characters()
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.EscapeSqlLiteral("a\0b"));
    }
}
