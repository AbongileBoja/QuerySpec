using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using QuerySpec.Core.Security;
using Xunit;

namespace QuerySpec.Core.Tests.Security;

/// <summary>
/// Tests for the strongly-typed predicate pathway on <see cref="RowLevelSecurityEngine"/>
/// as well as identifier validation and registration edge cases introduced by the
/// parameterized-SQL rewrite.
/// </summary>
[RequiresUnreferencedCode("Test materialises in-memory collections via Queryable.AsQueryable, whose IQueryable extension methods may rebind to IEnumerable extensions that are removed under trimming.")]
[RequiresDynamicCode("Test materialises in-memory collections via Queryable.AsQueryable, which can require runtime generic-type creation.")]
public class RowLevelSecurityEnginePredicateTests
{
    private sealed class Doc { public string Owner { get; set; } = string.Empty; public int Value { get; set; } }

    [Fact]
    public void GetPredicate_NoPolicy_DefaultThrows_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        var ctx = new RLSContext();
        Assert.Throws<InvalidOperationException>(() => engine.GetPredicate<Doc>("Doc", ctx));
    }

    [Fact]
    public void GetPredicate_NoPolicy_DefaultAllowAll_ReturnsNull()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.AllowAll);
        var ctx = new RLSContext();
        Assert.Null(engine.GetPredicate<Doc>("Doc", ctx));
    }

    [Fact]
    public void GetPredicate_NoPolicy_DefaultDenyAll_ReturnsConstantFalsePredicate()
    {
        var engine = new RowLevelSecurityEngine(RLSDefaultBehavior.DenyAll);
        var ctx = new RLSContext();

        var predicate = engine.GetPredicate<Doc>("Doc", ctx);

        Assert.NotNull(predicate);

        var docs = new[]
        {
            new Doc { Owner = "alice", Value = 1 },
            new Doc { Owner = "bob", Value = 2 },
        }.AsQueryable();

        Assert.Empty(docs.Where(predicate!).ToList());
    }

    [Fact]
    public void GetPredicate_PolicyWithoutPredicateFactory_DefaultThrows_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Doc",
            // PredicateFactory deliberately left null — only the SQL generator was configured.
        });

        Assert.Throws<InvalidOperationException>(
            () => engine.GetPredicate<Doc>("Doc", new RLSContext()));
    }

    [Fact]
    public void RegisterUnrestricted_ThenGetPredicate_ReturnsAlwaysTruePredicate()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterUnrestricted<Doc>("Doc");

        var predicate = engine.GetPredicate<Doc>("Doc", new RLSContext());

        Assert.NotNull(predicate);

        var docs = new[]
        {
            new Doc { Owner = "alice", Value = 1 },
            new Doc { Owner = "bob", Value = 2 },
        }.AsQueryable();

        Assert.Equal(2, docs.Where(predicate!).Count());
    }

    [Fact]
    public void GetPredicate_ReturnsConfiguredPredicate_AndFiltersCorrectly()
    {
        var engine = new RowLevelSecurityEngine();
        Func<RLSContext, Expression<Func<Doc, bool>>> factory =
            ctx => d => d.Owner == ctx.UserId;

        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Doc",
            PredicateFactory = factory
        });

        var predicate = engine.GetPredicate<Doc>("Doc", new RLSContext { UserId = "alice" });
        Assert.NotNull(predicate);

        var docs = new[]
        {
            new Doc { Owner = "alice", Value = 1 },
            new Doc { Owner = "bob", Value = 2 },
        }.AsQueryable();

        var filtered = docs.Where(predicate!).ToList();
        Assert.Single(filtered);
        Assert.Equal("alice", filtered[0].Owner);
    }

    [Fact]
    public void GetPredicate_IncompatibleType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Func<RLSContext, Expression<Func<Doc, bool>>> factory =
            _ => d => true;

        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Doc",
            PredicateFactory = factory
        });

        Assert.Throws<InvalidOperationException>(
            () => engine.GetPredicate<string>("Doc", new RLSContext()));
    }

    [Fact]
    public void SetPredicate_TypedHelper_FiltersCorrectly()
    {
        var engine = new RowLevelSecurityEngine();
        var policy = new RLSPolicy { ResourceType = "Doc" }
            .SetPredicate<Doc>(ctx => d => d.Owner == ctx.UserId);

        engine.RegisterPolicy(policy);

        var predicate = engine.GetPredicate<Doc>(
            "Doc",
            new RLSContext { UserId = "alice" });

        Assert.NotNull(predicate);
        var docs = new[]
        {
            new Doc { Owner = "alice" },
            new Doc { Owner = "bob" },
        }.AsQueryable();
        var filtered = docs.Where(predicate!).ToList();
        Assert.Single(filtered);
        Assert.Equal("alice", filtered[0].Owner);
    }

    [Fact]
    public void SetPredicate_NullFactory_Throws()
    {
        var policy = new RLSPolicy { ResourceType = "Doc" };

        Assert.Throws<ArgumentNullException>(() => policy.SetPredicate<Doc>(null!));
    }

    [Fact]
    public void SetPredicate_ReturnsSamePolicy_ForFluentChaining()
    {
        var policy = new RLSPolicy { ResourceType = "Doc" };
        var result = policy.SetPredicate<Doc>(_ => d => true);

        Assert.Same(policy, result);
    }

    [Fact]
    public void GenerateFilter_NullPolicyResult_FallsBackToDenyAll()
    {
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(new RLSPolicy
        {
            ResourceType = "Doc",
            FilterGenerator = _ => null!
        });

        var filter = engine.GenerateFilter("Doc", new RLSContext());
        Assert.Same(RLSFilter.DenyAll, filter);
    }

    [Fact]
    public void RegisterPolicy_NullOrMissingResourceType_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        Assert.Throws<ArgumentNullException>(() => engine.RegisterPolicy(null!));
        Assert.Throws<ArgumentException>(() => engine.RegisterPolicy(new RLSPolicy()));
    }

    [Fact]
    public void GenerateFilter_InvalidInput_Throws()
    {
        var engine = new RowLevelSecurityEngine();
        var ctx = new RLSContext();
        Assert.Throws<ArgumentException>(() => engine.GenerateFilter("", ctx));
        Assert.Throws<ArgumentNullException>(() => engine.GenerateFilter("Doc", null!));
    }

    [Theory]
    [InlineData("Department")]
    [InlineData("dept_name")]
    [InlineData("Schema.Column")]
    public void ValidateIdentifier_Accepts_WellFormedIdentifiers(string id)
    {
        Assert.Equal(id, RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Theory]
    [InlineData("1column")]
    [InlineData("col-name")]
    [InlineData("col name")]
    [InlineData("drop table users")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public void ValidateIdentifier_Rejects_MalformedIdentifiers(string id)
    {
        Assert.Throws<ArgumentException>(() => RowLevelSecurityEngine.ValidateIdentifier(id));
    }

    [Fact]
    public void EscapeSqlLiteral_EscapesAllSingleQuotes()
    {
        var escaped = RowLevelSecurityEngine.EscapeSqlLiteral("It's O'Brien");
        Assert.Equal("'It''s O''Brien'", escaped);
    }

    [Fact]
    public void OwnerPolicy_ParameterizesUserId()
    {
        var policy = RowLevelSecurityEngine.CreateOwnerBased("Owner");
        policy.ResourceType = "Doc";
        var engine = new RowLevelSecurityEngine();
        engine.RegisterPolicy(policy);

        var filter = engine.GenerateFilter("Doc", new RLSContext { UserId = "u1" });

        Assert.Equal("Owner = @rls_owner", filter.Sql);
        Assert.Equal("u1", filter.Parameters["rls_owner"]);
    }
}
