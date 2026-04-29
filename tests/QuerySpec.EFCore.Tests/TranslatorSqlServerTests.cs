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
/// Translator tests against a real SQL Server provider via Testcontainers (mcr.microsoft.com/mssql/server:2022-latest).
/// Validates that expression trees produced by <see cref="QuerySpecExpressionTranslator"/> are
/// accepted by the SQL Server dialect — including LIKE-pattern escaping for wildcard characters
/// (<c>[</c>, <c>_</c>, <c>%</c>) that SQL Server treats specially, decimal precision round-trips,
/// and DateTime ordering semantics.
/// </summary>
/// <remarks>
/// The collection fixture starts the container once per test assembly run; tests share the same
/// seeded data and each create their own short-lived <see cref="DbContext"/> for isolation.
/// These tests require Docker on the host.  They run on GitHub-hosted <c>ubuntu-latest</c>
/// (which ships Docker pre-installed) and are excluded from Windows runners that lack Docker via
/// the <c>RequiresDocker</c> trait guard on the collection fixture.
/// </remarks>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
[Collection("SqlServer")]
public class TranslatorSqlServerTests
{
    private readonly SqlServerFixture _fixture;

    public TranslatorSqlServerTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
    [RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
    private List<SqlServerWidget> Run(FilterSpec filter)
    {
        using var ctx = new SqlServerWidgetContext(_fixture.Options);
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

    /// <summary>Case-insensitive Contains maps to SQL <c>LIKE '%lph%'</c>.</summary>
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

    /// <summary>Case-insensitive StartsWith maps to SQL <c>LIKE 'alph%'</c>.</summary>
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

    /// <summary>Case-insensitive EndsWith maps to SQL <c>LIKE '%bet'</c>.</summary>
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
        Assert.True(result.Count >= 2);
    }

    /// <summary>IN-list maps to SQL <c>IN (...)</c>.</summary>
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

        Assert.Equal(4, result.Count);
        Assert.Single(result, w => w.Name == "alphabet");
        Assert.Single(result, w => w.Name == "50%_off");
        Assert.Single(result, w => w.Name == "[Special]");
        Assert.Single(result, w => w.Name == "Under_score");
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

        Assert.True(result.Count >= 2);
        Assert.All(result, w => Assert.True(w.Quantity > 0 && w.Price > 1m));
    }

    // ── SQL Server-specific tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SQL Server treats <c>%</c> as a LIKE wildcard.  A Contains filter whose value includes a
    /// literal <c>%</c> must match only rows containing that literal percent, not every row.
    /// Verifies the translator either escapes the character or uses a strategy that yields
    /// correct results on SQL Server.
    /// </summary>
    [Fact]
    public void Contains_LiteralPercent_MatchesOnlyLiteralRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "%",
            CaseSensitive = false,
        });

        Assert.All(result, w => Assert.Contains("%", w.Name, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, w => w.Name == "Alpha");
    }

    /// <summary>
    /// SQL Server treats <c>_</c> as a single-character LIKE wildcard.  A Contains filter with
    /// a literal underscore must match only rows containing that underscore character.
    /// </summary>
    [Fact]
    public void Contains_LiteralUnderscore_MatchesOnlyLiteralRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "_",
            CaseSensitive = false,
        });

        Assert.All(result, w => Assert.Contains("_", w.Name, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, w => w.Name == "Alpha");
    }

    /// <summary>
    /// SQL Server treats <c>[…]</c> as a character-class LIKE pattern.  A Contains filter with
    /// a literal bracket must match only rows containing that bracket, not act as a wildcard.
    /// </summary>
    [Fact]
    public void Contains_LiteralBracket_MatchesOnlyLiteralRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.Contains,
            Value = "[",
            CaseSensitive = false,
        });

        Assert.All(result, w => Assert.Contains("[", w.Name, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, w => w.Name == "Alpha");
    }

    /// <summary>
    /// <c>decimal(18,4)</c> precision round-trips through SQL Server without truncation.
    /// The translator must parameterise the value correctly so the stored and queried values
    /// match to 4 decimal places.
    /// </summary>
    [Fact]
    public void DecimalPrecision_RoundTrips_ThroughSqlServer()
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
    /// A <c>Between</c> filter on a <c>DateTime</c> column uses SQL Server's native date
    /// comparison.  Rows outside the range must not appear in the result regardless of
    /// <c>DATEPART</c>-based rewriting that older SQL Server versions required.
    /// </summary>
    [Fact]
    public void DateTime_Between_ExcludesRowsOutsideRange()
    {
        var result = Run(new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.Between,
            Value = new DateTime(2024, 1, 1),
            ValueTo = new DateTime(2024, 6, 30),
        });

        Assert.All(result, w =>
        {
            Assert.True(w.CreatedAt >= new DateTime(2024, 1, 1));
            Assert.True(w.CreatedAt <= new DateTime(2024, 6, 30));
        });
        Assert.DoesNotContain(result, w => w.CreatedAt.Year == 2023);
        Assert.DoesNotContain(result, w => w.CreatedAt >= new DateTime(2024, 7, 1));
    }

    /// <summary>
    /// <c>DateInRange</c> uses <c>TemporalStart</c>/<c>TemporalEnd</c>.  This exercises the
    /// SQL Server-specific date range path end-to-end.
    /// </summary>
    [Fact]
    public void DateInRange_Translates_To_Sql()
    {
        var result = Run(new FilterSpec
        {
            Field = "CreatedAt",
            Operator = FilterOperator.DateInRange,
            TemporalStart = new DateTime(2024, 1, 1),
            TemporalEnd = new DateTime(2024, 3, 31),
        });

        Assert.All(result, w =>
        {
            Assert.True(w.CreatedAt >= new DateTime(2024, 1, 1));
            Assert.True(w.CreatedAt <= new DateTime(2024, 3, 31));
        });
    }

    /// <summary>StartsWith with a literal underscore in the value must not act as a wildcard.</summary>
    [Fact]
    public void StartsWith_LiteralUnderscore_MatchesOnlyLiteralRows()
    {
        var result = Run(new FilterSpec
        {
            Field = "Name",
            Operator = FilterOperator.StartsWith,
            Value = "Under_",
            CaseSensitive = false,
        });

        Assert.All(result, w => Assert.StartsWith("Under_", w.Name, StringComparison.OrdinalIgnoreCase));
    }
}
