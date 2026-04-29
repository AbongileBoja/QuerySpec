using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using VerifyXunit;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// SQL snapshot tests for the EFCore translator. Each test converts a FilterSpec to an
/// IQueryable, calls ToQueryString(), and verifies the output against a committed
/// .verified.txt snapshot under SqlSnapshots/.
/// Provider: SQLite in-memory — provider-stable, no Docker, hermetic.
/// </summary>
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public sealed class SqlSnapshotTests : IDisposable
{
    private static readonly string SnapshotDirectory = GetSnapshotDirectory();

    private static string GetSnapshotDirectory()
    {
        var projectDir = typeof(SqlSnapshotTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Verify.ProjectDirectory")
            .Value!;
        return Path.Combine(projectDir, "SqlSnapshots");
    }

    [RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
    [RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
    private sealed class SnapContext : DbContext
    {
        public DbSet<SnapWidget> Widgets => Set<SnapWidget>();
        public SnapContext(DbContextOptions<SnapContext> options) : base(options) { }
    }

    private sealed class SnapWidget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
    }

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SnapContext> _options;

    public SqlSnapshotTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SnapContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new SnapContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private string Sql(FilterSpec filter)
    {
        using var ctx = new SnapContext(_options);
        return QuerySpecExpressionTranslator.ApplyFilter(ctx.Widgets.AsQueryable(), filter).ToQueryString();
    }

    [Fact]
    public Task Equal_SimpleString_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Name),
            Operator = FilterOperator.Equal,
            Value = "Alpha",
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task NotEqual_SimpleString_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Name),
            Operator = FilterOperator.NotEqual,
            Value = "Alpha",
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task In_IntArray_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Id),
            Operator = FilterOperator.In,
            Value = new[] { 1, 2, 3 },
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task NotIn_IntArray_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Id),
            Operator = FilterOperator.NotIn,
            Value = new[] { 1, 2, 3 },
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task StringContains_CaseSensitive_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Name),
            Operator = FilterOperator.Contains,
            Value = "lph",
            CaseSensitive = true,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task StringContains_CaseInsensitive_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Name),
            Operator = FilterOperator.Contains,
            Value = "lph",
            CaseSensitive = false,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task Between_NullableDecimal_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Price),
            Operator = FilterOperator.Between,
            Value = 1m,
            ValueTo = 100m,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task IsNull_NullableDecimal_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Price),
            Operator = FilterOperator.IsNull,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task IsNotNull_NullableDecimal_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Price),
            Operator = FilterOperator.IsNotNull,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task NestedAnd_TwoPredicates_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Quantity),
            Operator = FilterOperator.GreaterThan,
            Value = 0,
            Logic = LogicalOperator.And,
            Filters = new List<FilterSpec>
            {
                new() { Field = nameof(SnapWidget.Price), Operator = FilterOperator.GreaterThan, Value = 1m },
            },
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task OrChain_TwoPredicates_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Quantity),
            Operator = FilterOperator.Equal,
            Value = 0,
            Logic = LogicalOperator.Or,
            Filters = new List<FilterSpec>
            {
                new() { Field = nameof(SnapWidget.Name), Operator = FilterOperator.Equal, Value = "Alpha" },
            },
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }

    [Fact]
    public Task GreaterThan_Int_ProducesExpectedSql()
    {
        var sql = Sql(new FilterSpec
        {
            Field = nameof(SnapWidget.Quantity),
            Operator = FilterOperator.GreaterThan,
            Value = 5,
        });
        return Verifier.Verify(sql).UseDirectory(SnapshotDirectory);
    }
}
