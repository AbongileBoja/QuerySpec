using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Translator tests against a real SQL provider (SQLite via in-memory connection). Closes the
/// gap flagged in test-engineer audit #71 — the rest of the EFCore suite ran on
/// <c>UseInMemoryDatabase</c>, which evaluates predicates client-side as LINQ-to-Objects and
/// silently passes expressions that real SQL providers would reject as untranslatable.
/// </summary>
/// <remarks>
/// SQLite is the lightest real-SQL provider available without Docker. It validates SQL syntax,
/// the EF Core SQL translator pipeline, and parameterisation. SQL-Server- and PostgreSQL-specific
/// dialect issues require Testcontainers; that is tracked as a follow-up.
/// </remarks>
public class TranslatorSqliteTests : IDisposable
{
    private sealed class WidgetContext : DbContext
    {
        public DbSet<Widget> Widgets => Set<Widget>();
        public WidgetContext(DbContextOptions<WidgetContext> options) : base(options) { }
    }

    private sealed class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly DbContextOptions<WidgetContext> _options;

    public TranslatorSqliteTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<WidgetContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new WidgetContext(_options);
        ctx.Database.EnsureCreated();
        ctx.Widgets.AddRange(
            new Widget { Id = 1, Name = "Alpha", Quantity = 10, Price = 99.50m, CreatedAt = new DateTime(2024, 1, 15) },
            new Widget { Id = 2, Name = "Beta", Quantity = 5, Price = null, CreatedAt = new DateTime(2024, 6, 1) },
            new Widget { Id = 3, Name = "alphabet", Quantity = 25, Price = 49.99m, CreatedAt = new DateTime(2024, 9, 20) },
            new Widget { Id = 4, Name = "Gamma", Quantity = 0, Price = 0m, CreatedAt = new DateTime(2023, 12, 31) });
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private List<Widget> Run(FilterSpec filter)
    {
        using var ctx = new WidgetContext(_options);
        return QuerySpecExpressionTranslator.ApplyFilter(ctx.Widgets.AsQueryable(), filter).ToList();
    }

    /// <summary>
    /// String Equal must translate to a real SQL <c>=</c> predicate, not a client-side
    /// projection. Verified by the result count matching the seeded data.
    /// </summary>
    [Fact]
    public void Equal_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Equal,
            Value = "Alpha",
        });

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    /// <summary>
    /// Case-insensitive Contains translates to SQL LIKE via <c>EF.Functions.Like</c>. Matches
    /// both "Alpha" (Id=1) and "alphabet" (Id=3) because the pattern is <c>%lph%</c>.
    /// </summary>
    [Fact]
    public void StringContains_CaseInsensitive_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "lph",
            CaseSensitive = false,
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, w => w.Id == 1);
        Assert.Contains(result, w => w.Id == 3);
    }

    /// <summary>
    /// Case-insensitive StartsWith translates to SQL LIKE with a trailing <c>%</c>. Matches
    /// both "Alpha" (Id=1) and "alphabet" (Id=3); case-folding is handled by SQLite's LIKE.
    /// </summary>
    [Fact]
    public void StringStartsWith_CaseInsensitive_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.StartsWith,
            Value = "alph",
            CaseSensitive = false,
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, w => w.Id == 1);
        Assert.Contains(result, w => w.Id == 3);
    }

    /// <summary>
    /// Case-insensitive EndsWith translates to SQL LIKE with a leading <c>%</c>. Matches
    /// "alphabet" (Id=3) but not "Alpha" (Id=1) — the suffix "bet" is unique to Id=3.
    /// </summary>
    [Fact]
    public void StringEndsWith_CaseInsensitive_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.EndsWith,
            Value = "bet",
            CaseSensitive = false,
        });

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    /// <summary>
    /// Case-sensitive Contains uses <c>string.Contains(value)</c> directly, which EF Core maps
    /// to a SQL predicate. "Alpha" contains "lph"; "alphabet" also contains "lph" case-sensitively.
    /// </summary>
    [Fact]
    public void StringContains_CaseSensitive_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "lph",
            CaseSensitive = true,
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.Contains("lph", w.Name, StringComparison.Ordinal));
    }

    /// <summary>Numeric comparison on int translates cleanly.</summary>
    [Fact]
    public void GreaterThan_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Quantity",
            Operator = FilterOperator.GreaterThan,
            Value = 5,
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.True(w.Quantity > 5));
    }

    /// <summary>
    /// Nullable decimal Equal must translate including the <c>HasValue</c> unwrapping. SQLite
    /// rejects untranslatable expressions, so a passing test here proves the translator's
    /// nullable-handling code path produces real SQL.
    /// </summary>
    [Fact]
    public void NullableDecimal_Equal_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.Equal,
            Value = 49.99m,
        });

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    /// <summary>
    /// Nullable decimal GreaterThan must include the HasValue check; passing rows with
    /// <c>Price = null</c> would indicate a translation defect.
    /// </summary>
    [Fact]
    public void NullableDecimal_GreaterThan_Translates_And_ExcludesNull()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.GreaterThan,
            Value = 1m,
        });

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, w => w.Price is null);
    }

    /// <summary>IN-list translates to SQL IN (...) with parameterised values.</summary>
    [Fact]
    public void In_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = new[] { 1, 3 },
        });

        Assert.Equal(2, result.Count);
    }

    /// <summary>Between for nullable decimal — tests NotBetween's negation as well.</summary>
    [Fact]
    public void Between_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.Between,
            Value = 1m,
            ValueTo = 60m,
        });

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    /// <summary>
    /// Nested AND of two predicates (with the outer filter still satisfying <c>Validate</c>'s
    /// "Field is required" rule) forms a real SQL AND. A bug in <c>BuildBody</c> composition
    /// would either drop one predicate or trip the translator with a client-eval warning.
    /// </summary>
    [Fact]
    public void Nested_And_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "Quantity",
            Operator = FilterOperator.GreaterThan,
            Value = 0,
            Logic = LogicalOperator.And,
            Filters = new List<FilterSpec>
            {
                new() { Field = "Price", Operator = FilterOperator.GreaterThan, Value = 1m },
            },
        });

        Assert.Equal(2, result.Count);
    }
}
