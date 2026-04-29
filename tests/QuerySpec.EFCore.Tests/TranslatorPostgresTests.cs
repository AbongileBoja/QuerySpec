using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using QuerySpec.EFCore.Tests.Fixtures;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Translator tests against a real PostgreSQL 16 provider via Testcontainers (postgres:16-alpine).
/// Validates that expression trees produced by <see cref="QuerySpecExpressionTranslator"/> are
/// accepted by the Npgsql dialect and produce correct SQL, including case-insensitive ILIKE
/// behaviour and DateTime semantics with UTC-typed timestamps.
/// </summary>
/// <remarks>
/// The collection fixture starts the container once per test assembly run; tests share the same
/// seeded data and each create their own short-lived <see cref="DbContext"/> for isolation.
/// These tests require Docker on the host.  They run on GitHub-hosted <c>ubuntu-latest</c>
/// which ships Docker pre-installed.
/// </remarks>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
[Collection("Postgres")]
public class TranslatorPostgresTests
{
    private readonly PostgresFixture _fixture;

    public TranslatorPostgresTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
    [RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
    private List<PostgresWidget> Run(FilterSpec filter)
    {
        using var ctx = new PostgresWidgetContext(_fixture.Options);
        return QuerySpecExpressionTranslator.ApplyFilter(ctx.Widgets.AsQueryable(), filter).ToList();
    }

    // ── Parity tests mirroring TranslatorSqliteTests ─────────────────────────────────────────────

    /// <summary>String Equal maps to SQL <c>=</c>.</summary>
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
    /// Case-insensitive Contains.  On PostgreSQL the translator emits <c>ToLower()</c> + LIKE,
    /// which Npgsql maps to <c>lower(col) LIKE lower('%lph%')</c>.  Both "Alpha" and "alphabet"
    /// must match.
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

    /// <summary>Case-insensitive StartsWith maps to a SQL LIKE with trailing wildcard.</summary>
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

    /// <summary>Case-insensitive EndsWith maps to a SQL LIKE with leading wildcard.</summary>
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

    /// <summary>Numeric GreaterThan produces a real SQL <c>&gt;</c> predicate.</summary>
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

    /// <summary>Nullable decimal Equal uses SQL IS NULL semantics for null rows.</summary>
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

    /// <summary>Nullable decimal GreaterThan excludes rows where Price IS NULL.</summary>
    [Fact]
    public void NullableDecimal_GreaterThan_Translates_And_ExcludesNull()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.GreaterThan,
            Value = 1m,
        });

        Assert.DoesNotContain(result, w => w.Price is null);
        Assert.Equal(2, result.Count);
    }

    /// <summary>IN-list maps to SQL <c>= ANY(ARRAY[…])</c> on PostgreSQL.</summary>
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

    /// <summary>Between produces a closed SQL range predicate.</summary>
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

    /// <summary>Nested AND composes two predicates with SQL AND.</summary>
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
        Assert.All(result, w => Assert.True(w.Quantity > 0 && w.Price > 1m));
    }

    // ── PostgreSQL-specific tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Case-insensitive Equal on PostgreSQL.  PostgreSQL string comparison is case-sensitive by
    /// default; the translator lowers both sides, so <c>Equal("alpha")</c> must match
    /// "Alpha" (Id=1).
    /// </summary>
    [Fact]
    public void Equal_CaseInsensitive_LowerBothSides_MatchesCorrectly()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "ALPHA",
            CaseSensitive = false,
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, w => w.Id == 1);
        Assert.Contains(result, w => w.Id == 3);
    }

    /// <summary>
    /// Case-sensitive Contains on PostgreSQL must not match rows that only differ by case.
    /// "Alpha" contains "lph" case-sensitively; "Beta" and "Gamma" do not.
    /// </summary>
    [Fact]
    public void StringContains_CaseSensitive_DoesNotMatchCaseVariants()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "lph",
            CaseSensitive = true,
        });

        Assert.All(result, w => Assert.Contains("lph", w.Name, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>decimal(18,4)</c> precision round-trips through PostgreSQL's <c>numeric</c> type
    /// without truncation.
    /// </summary>
    [Fact]
    public void DecimalPrecision_RoundTrips_ThroughPostgres()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.Equal,
            Value = 99.5000m,
        });

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(99.50m, result[0].Price);
    }

    /// <summary>
    /// DateTime ordering on PostgreSQL stores timestamps as <c>timestamp with time zone</c>
    /// (UTC). A <c>Between</c> filter must honour the UTC boundary correctly and not include
    /// rows outside the range.
    /// </summary>
    [Fact]
    public void DateTime_Between_RespectsUtcBoundaries()
    {
        var result = Run(new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.Between,
            Value = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ValueTo = new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc),
        });

        Assert.All(result, w =>
        {
            Assert.True(w.CreatedAt >= new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.True(w.CreatedAt <= new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc));
        });
        Assert.DoesNotContain(result, w => w.CreatedAt.Year == 2023);
    }

    /// <summary>
    /// <c>DateInRange</c> uses <c>TemporalStart</c>/<c>TemporalEnd</c>.  This exercises the
    /// PostgreSQL date range path end-to-end.
    /// </summary>
    [Fact]
    public void DateInRange_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TemporalEnd = new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc),
        });

        Assert.All(result, w =>
        {
            Assert.True(w.CreatedAt >= new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.True(w.CreatedAt <= new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc));
        });
    }

    /// <summary>
    /// <c>IsNull</c> on the nullable Price column must return exactly the row where Price is
    /// <c>NULL</c> in the database — Id=2.
    /// </summary>
    [Fact]
    public void IsNull_ReturnsOnlyNullRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.IsNull,
        });

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    /// <summary>
    /// <c>IsNotNull</c> must exclude the row where Price is NULL.
    /// </summary>
    [Fact]
    public void IsNotNull_ExcludesNullRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.IsNotNull,
        });

        Assert.DoesNotContain(result, w => w.Price is null);
        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// NotEqual must exclude only the matching row; it must not accidentally include NULL rows
    /// (NULL != value is NULL in SQL, not TRUE).
    /// </summary>
    [Fact]
    public void NotEqual_ExcludesNullRows_SqlNullSemantics()
    {
        var result = Run(new FilterSpec
        {
            Field = "Price",
            Operator = FilterOperator.NotEqual,
            Value = 49.99m,
        });

        Assert.DoesNotContain(result, w => w.Price == 49.99m);
    }
}
