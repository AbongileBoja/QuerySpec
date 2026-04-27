using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Comprehensive operator coverage for <see cref="QuerySpecExpressionTranslator"/>.
/// Exercises every operator branch the translator supports, against in-memory IQueryable
/// (sufficient to validate predicate correctness without the EF Core translation layer).
/// </summary>
public class TranslatorOperatorTests
{
    private sealed class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal? Salary { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Address? Address { get; set; }
    }

    private sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    private static IQueryable<Entity> Seed() => new[]
    {
        new Entity { Id = 1, Name = "Alice", Age = 30, Salary = 5000m, Email = "a@x.io",
            CreatedAt = new DateTime(2024, 1, 10), Address = new Address { City = "Cape Town" } },
        new Entity { Id = 2, Name = "Bob", Age = 25, Salary = null, Email = null,
            CreatedAt = new DateTime(2024, 3, 15), DeletedAt = new DateTime(2025, 1, 1) },
        new Entity { Id = 3, Name = "Carol", Age = 42, Salary = 8000m, Email = "",
            CreatedAt = new DateTime(2023, 11, 1), Address = new Address { City = "Johannesburg" } },
        new Entity { Id = 4, Name = "Dave", Age = 35, Salary = 6500m, Email = "dave@y.io",
            CreatedAt = new DateTime(2024, 6, 5), Address = new Address { City = "cape town" } },
    }.AsQueryable();

    private static List<Entity> Run(AdvancedFilterExpression filter) =>
        QuerySpecExpressionTranslator.ApplyFilter(Seed(), filter).ToList();

    // ── equality / inequality ─────────────────────────────────────────────────

    [Fact]
    public void Equal_MatchesExactValue()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Equal, Value = "Alice" });
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void NotEqual_ExcludesMatchingValue()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.NotEqual, Value = "Alice" });
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, e => e.Name == "Alice");
    }

    // ── comparisons ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(FilterOperator.GreaterThan, 30, new[] { 3, 4 })]
    [InlineData(FilterOperator.GreaterThanOrEqual, 30, new[] { 1, 3, 4 })]
    [InlineData(FilterOperator.LessThan, 30, new[] { 2 })]
    [InlineData(FilterOperator.LessThanOrEqual, 30, new[] { 1, 2 })]
    public void NumericComparisons_ReturnExpectedIds(FilterOperator op, int pivot, int[] expected)
    {
        var result = Run(new AdvancedFilterExpression { Field = "Age", Operator = op, Value = pivot });
        Assert.Equal(expected.OrderBy(x => x), result.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public void GreaterThan_OnNullableValueType_SkipsNullValues()
    {
        // Bob has Salary == null; must not pass a >= 0 comparison.
        var result = Run(new AdvancedFilterExpression { Field = "Salary", Operator = FilterOperator.GreaterThanOrEqual, Value = 0m });
        Assert.DoesNotContain(result, e => e.Id == 2);
        Assert.Equal(3, result.Count);
    }

    // ── string operators ──────────────────────────────────────────────────────

    [Fact]
    public void Contains_CaseInsensitiveByDefault()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Contains, Value = "ar" });
        Assert.Single(result);
        Assert.Equal("Carol", result[0].Name);
    }

    /// <summary>
    /// Verifies the renamed <see cref="FilterOperator.ContainsCaseInsensitive"/> behaves identically
    /// to the obsolete snake-case alias they share an underlying value with — both must continue to
    /// translate through the same predicate path until <c>Contains_CaseInsensitive</c> is removed in 3.0.
    /// </summary>
    [Fact]
    public void ContainsCaseInsensitive_AliasAndRenamedMember_ProduceSameResults()
    {
        var renamed = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.ContainsCaseInsensitive, Value = "ali" });
#pragma warning disable CS0618
        var legacy = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Contains_CaseInsensitive, Value = "ali" });
#pragma warning restore CS0618
        Assert.Equal(renamed.Select(e => e.Id).OrderBy(x => x), legacy.Select(e => e.Id).OrderBy(x => x));
        Assert.Single(renamed);
        Assert.Equal("Alice", renamed[0].Name);
    }

    [Fact]
    public void Contains_CaseSensitive_RespectsFlag()
    {
        var insensitive = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Contains, Value = "a", CaseSensitive = false });
        var sensitive = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.Contains, Value = "A", CaseSensitive = true });
        // Alice, Carol, Dave contain 'a'/'A'; Bob does not.
        Assert.Equal(3, insensitive.Count);
        Assert.DoesNotContain(insensitive, e => e.Name == "Bob");
        // Case-sensitive match on 'A' is only Alice.
        Assert.Single(sensitive);
        Assert.Equal("Alice", sensitive[0].Name);
    }

    [Fact]
    public void NotContains_Inverts()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.NotContains, Value = "ar" });
        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, e => e.Name == "Carol");
    }

    [Fact]
    public void StartsWith_Matches()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.StartsWith, Value = "Ca" });
        Assert.Single(result);
        Assert.Equal("Carol", result[0].Name);
    }

    [Fact]
    public void EndsWith_Matches()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Name", Operator = FilterOperator.EndsWith, Value = "ve" });
        Assert.Single(result);
        Assert.Equal("Dave", result[0].Name);
    }

    [Fact]
    public void Contains_OnNullString_ReturnsFalseNotThrow()
    {
        // Bob.Email is null; the translator wraps calls with a null check and must not throw.
        var result = Run(new AdvancedFilterExpression { Field = "Email", Operator = FilterOperator.Contains, Value = "@" });
        Assert.DoesNotContain(result, e => e.Id == 2);
        Assert.DoesNotContain(result, e => e.Id == 3); // Carol has empty email
    }

    [Fact]
    public void Regex_ValidPattern_Matches()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Email", Operator = FilterOperator.Regex, Value = @"^[a-z]+@[a-z]+\.io$" });
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.NotNull(e.Email));
    }

    [Fact]
    public void Regex_InvalidPattern_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Run(new AdvancedFilterExpression { Field = "Email", Operator = FilterOperator.Regex, Value = "[unclosed" }));
        Assert.Contains("regex", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── IN / NotIn ────────────────────────────────────────────────────────────

    [Fact]
    public void In_MatchesAnyOf()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = new[] { 1, 3 }
        });
        Assert.Equal(new[] { 1, 3 }, result.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public void NotIn_ExcludesMatches()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Id",
            Operator = FilterOperator.NotIn,
            Value = new[] { 1, 3 }
        });
        Assert.Equal(new[] { 2, 4 }, result.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public void In_EmptyCollection_ReturnsNoRows()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = Array.Empty<int>()
        });
        Assert.Empty(result);
    }

    [Fact]
    public void In_ExceedsMaxItemCap_Throws()
    {
        var huge = Enumerable.Range(0, 500).ToArray();
        var ex = Assert.Throws<ArgumentException>(() =>
            Run(new AdvancedFilterExpression { Field = "Id", Operator = FilterOperator.In, Value = huge }));
        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Between / NotBetween ─────────────────────────────────────────────────

    [Fact]
    public void Between_IsInclusive()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.Between,
            Value = 25,
            ValueTo = 35
        });
        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.InRange(e.Age, 25, 35));
    }

    [Fact]
    public void NotBetween_InvertsInclusiveRange()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.NotBetween,
            Value = 25,
            ValueTo = 35
        });
        Assert.Single(result);
        Assert.Equal(42, result[0].Age);
    }

    // ── null / empty ──────────────────────────────────────────────────────────

    [Fact]
    public void IsNull_MatchesNullableValueTypeNulls()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Salary", Operator = FilterOperator.IsNull });
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void IsNotNull_InvertsNullCheck()
    {
        var result = Run(new AdvancedFilterExpression { Field = "DeletedAt", Operator = FilterOperator.IsNotNull });
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void IsEmpty_MatchesEmptyString()
    {
        var result = Run(new AdvancedFilterExpression { Field = "Email", Operator = FilterOperator.IsEmpty });
        // Carol has empty email; Bob has null (which the isNullable branch includes via OrElse).
        Assert.Contains(result, e => e.Id == 3);
    }

    // ── date operators ────────────────────────────────────────────────────────

    [Fact]
    public void DateInRange_MatchesInclusiveWindow()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2024, 1, 1),
            TemporalEnd = new DateTime(2024, 12, 31)
        });
        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.InRange(e.CreatedAt, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)));
    }

    [Fact]
    public void DateAfter_StrictGreaterThan()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateAfter,
            Value = new DateTime(2024, 3, 1)
        });
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.True(e.CreatedAt > new DateTime(2024, 3, 1)));
    }

    [Fact]
    public void DateBefore_StrictLessThan()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateBefore,
            Value = new DateTime(2024, 1, 1)
        });
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void DateEquals_MatchesOnDayRegardlessOfTime()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateEquals,
            Value = "2024-01-10T08:30:00"
        });
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    // ── logical composition ──────────────────────────────────────────────────

    [Fact]
    public void LogicalAnd_OfTwoChildren()
    {
        // The outer filter carries Field/Operator (required by Validate), nested adds the AND child.
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.GreaterThan,
            Value = 25,
            Logic = LogicalOperator.And,
            Filters = new()
            {
                new() { Field = "Name", Operator = FilterOperator.StartsWith, Value = "A" }
            }
        });
        Assert.Single(result);
        Assert.Equal("Alice", result[0].Name);
    }

    [Fact]
    public void LogicalOr_OfTwoChildren()
    {
        var result = Run(new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "Alice",
            Logic = LogicalOperator.Or,
            Filters = new()
            {
                new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Bob" }
            }
        });
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Logical_DepthLimit_Throws()
    {
        // Build a filter deeper than MaxFilterDepth (10).
        var deepest = new AdvancedFilterExpression { Field = "Id", Operator = FilterOperator.Equal, Value = 1 };
        for (var i = 0; i < 15; i++)
        {
            deepest = new AdvancedFilterExpression
            {
                Field = "Id",
                Operator = FilterOperator.Equal,
                Value = 1,
                Logic = LogicalOperator.And,
                Filters = new() { deepest }
            };
        }
        // The outer filter must have Field=<something> or a nested child to pass validation.
        Assert.Throws<ArgumentException>(() => Run(deepest));
    }

    // ── property path navigation ──────────────────────────────────────────────

    [Fact]
    public void NestedPropertyPath_Resolves()
    {
        // Filter entities with a populated Address so we isolate path-resolution behavior
        // from null-propagation semantics (which differ between in-memory LINQ and EF Core).
        var filter = new AdvancedFilterExpression
        {
            Field = "Address.City",
            Operator = FilterOperator.Equal,
            Value = "Cape Town",
            CaseSensitive = false
        };

        var withAddresses = Seed().Where(e => e.Address != null).AsQueryable();
        var result = QuerySpecExpressionTranslator.ApplyFilter(withAddresses, filter).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void UnknownProperty_Throws()
    {
        Assert.Throws<ArgumentException>(() => Run(new AdvancedFilterExpression
        {
            Field = "DoesNotExist",
            Operator = FilterOperator.Equal,
            Value = "x"
        }));
    }

    [Fact]
    public void InvalidFieldName_FailsValidation()
    {
        Assert.Throws<ArgumentException>(() => Run(new AdvancedFilterExpression
        {
            Field = "Name; DROP TABLE Users",
            Operator = FilterOperator.Equal,
            Value = "x"
        }));
    }

    // ── pass-through ──────────────────────────────────────────────────────────

    [Fact]
    public void NullFilter_ReturnsSourceUnchanged()
    {
        var source = Seed();
        var result = QuerySpecExpressionTranslator.ApplyFilter(source, null);
        Assert.Same(source, result);
    }

    [Fact]
    public void ApplyAggregation_NullRequest_IsPassThrough()
    {
        var source = Seed();
#pragma warning disable CS0618
        var result = QuerySpecExpressionTranslator.ApplyAggregation(source, null);
#pragma warning restore CS0618
        Assert.Same(source, result);
    }

    [Fact]
    public void ApplyAggregation_NonNullRequest_Throws_NotImplemented()
    {
        var source = Seed();
        var request = new AggregationRequest();
#pragma warning disable CS0618
        Assert.Throws<NotImplementedException>(() =>
            QuerySpecExpressionTranslator.ApplyAggregation(source, request));
#pragma warning restore CS0618
    }

    // ── EF Core in-memory provider ────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task Translator_IntegratesWithEfCoreInMemoryProvider()
    {
        using var ctx = new TestDb();
        ctx.Items.AddRange(Seed());
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filter = new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.GreaterThanOrEqual,
            Value = 30
        };

        var filtered = QuerySpecExpressionTranslator.ApplyFilter(ctx.Items, filter);
        var ids = await filtered.Select(e => e.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 1, 3, 4 }, ids.OrderBy(x => x));
    }

    private sealed class TestDb : DbContext
    {
        public DbSet<Entity> Items => Set<Entity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseInMemoryDatabase(Guid.NewGuid().ToString());
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Entity>(e =>
            {
                e.OwnsOne(x => x.Address);
            });
        }
    }
}
