using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Unit tests for QuerySpecExpressionTranslator.
/// </summary>
public class QuerySpecExpressionTranslatorTests
{
    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Tests that ApplyFilter filters by Equal operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_Equal()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John"
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("John", result[0].Name);
    }

    /// <summary>Tests that ApplyFilter filters by GreaterThan operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_GreaterThan()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.GreaterThan,
            Value = 28
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(30, result[0].Age);
    }

    /// <summary>Tests that ApplyFilter filters by Contains operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_Contains()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "oh"
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("John", result[0].Name);
    }

    /// <summary>Tests that ApplyFilter filters by In operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_In()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 3, Name = "Bob", Age = 35, Email = "bob@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.In,
            Value = new[] { 25, 35 }
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Age == 25);
        Assert.Contains(result, x => x.Age == 35);
    }

    /// <summary>Tests that ApplyFilter filters by Between operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_Between()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 3, Name = "Bob", Age = 35, Email = "bob@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Age",
            Operator = FilterOperator.Between,
            Value = 28,
            ValueTo = 35
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.Age >= 28 && x.Age <= 35));
    }

    /// <summary>Tests that ApplyFilter filters by Logical And operator.</summary>
    [Fact]
    public async Task ApplyFilter_Should_Filter_By_Logical_And()
    {
        // Arrange
        using var context = new TestDbContext();
        context.TestEntities.AddRange(
            new TestEntity { Id = 1, Name = "John", Age = 25, Email = "john@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 2, Name = "Jane", Age = 30, Email = "jane@test.com", CreatedAt = DateTime.UtcNow },
            new TestEntity { Id = 3, Name = "John", Age = 35, Email = "john2@test.com", CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var filter = new AdvancedFilterExpression
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "John",
            Logic = LogicalOperator.And,
            Filters = new List<AdvancedFilterExpression>
            {
                new AdvancedFilterExpression
                {
                    Field = "Age",
                    Operator = FilterOperator.GreaterThan,
                    Value = 30
                }
            }
        };

        // Act
        var query = context.TestEntities.AsQueryable();
        var filtered = QuerySpecExpressionTranslator.ApplyFilter(query, filter);
        var result = await filtered.ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("John", result[0].Name);
        Assert.Equal(35, result[0].Age);
    }
}
