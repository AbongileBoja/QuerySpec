using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CsCheck;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests.Properties;

[Trait("Category", "PropertyBased")]
[RequiresUnreferencedCode("Test exercises QuerySpecExpressionTranslator, which requires reflection metadata for entity property resolution.")]
[RequiresDynamicCode("Test exercises QuerySpecExpressionTranslator, which compiles expression trees at runtime.")]
public sealed class FilterTranslatorPropertyTests : IDisposable
{
    private sealed class SampleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal? Score { get; set; }
    }

    [RequiresUnreferencedCode("EF Core DbContext is not fully compatible with trimming.")]
    [RequiresDynamicCode("EF Core DbContext is not fully compatible with NativeAOT.")]
    private sealed class SampleContext : DbContext
    {
        public DbSet<SampleEntity> Entities => Set<SampleEntity>();
        public SampleContext(DbContextOptions<SampleContext> options) : base(options) { }
    }

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SampleContext> _options;

    public FilterTranslatorPropertyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<SampleContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new SampleContext(_options);
        ctx.Database.EnsureCreated();
        ctx.Entities.AddRange(
            new SampleEntity { Id = 1, Name = "Alice", Age = 30, IsActive = true, CreatedAt = new DateTime(2024, 1, 1), Score = 9.5m },
            new SampleEntity { Id = 2, Name = "Bob", Age = 25, IsActive = false, CreatedAt = new DateTime(2024, 6, 15), Score = null },
            new SampleEntity { Id = 3, Name = "Carol", Age = 40, IsActive = true, CreatedAt = new DateTime(2023, 12, 31), Score = 7.0m });
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private static readonly FilterOperator[] ImplementedStringOperators =
    [
        FilterOperator.Equal, FilterOperator.NotEqual,
        FilterOperator.Contains, FilterOperator.NotContains,
        FilterOperator.StartsWith, FilterOperator.EndsWith,
        FilterOperator.StringMatchCase, FilterOperator.StringMatchIgnoreCase,
        FilterOperator.ContainsCaseInsensitive,
        FilterOperator.IsNull, FilterOperator.IsNotNull,
        FilterOperator.IsEmpty, FilterOperator.IsNotEmpty
    ];

    private static readonly FilterOperator[] ImplementedIntOperators =
    [
        FilterOperator.Equal, FilterOperator.NotEqual,
        FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual,
        FilterOperator.LessThan, FilterOperator.LessThanOrEqual,
        FilterOperator.IsNull, FilterOperator.IsNotNull
    ];

    private static readonly FilterOperator[] ImplementedBoolOperators =
    [
        FilterOperator.Equal, FilterOperator.NotEqual
    ];

    private static readonly FilterOperator[] UnimplementedOperators =
    [
        FilterOperator.AnyMatch, FilterOperator.AllMatch,
        FilterOperator.CountGreaterThan, FilterOperator.CountLessThan,
        FilterOperator.FullTextSearch, FilterOperator.GeoDistance, FilterOperator.GeoWithin,
        FilterOperator.RelativeDate, FilterOperator.BitwiseAnd, FilterOperator.BitwiseOr,
        FilterOperator.Custom
    ];

    private static Gen<FilterSpec> StringFieldFilterGen() =>
        Gen.Select(
            Gen.OneOfConst(ImplementedStringOperators),
            Gen.String,
            (op, value) => new FilterSpec
            {
                Field = "Name",
                Operator = op,
                Value = value,
                CaseSensitive = false
            });

    private static Gen<FilterSpec> IntFieldFilterGen() =>
        Gen.Select(
            Gen.OneOfConst(ImplementedIntOperators),
            Gen.Int,
            (op, value) => new FilterSpec
            {
                Field = "Age",
                Operator = op,
                Value = value
            });

    private static Gen<FilterSpec> BoolFieldFilterGen() =>
        Gen.Select(
            Gen.OneOfConst(ImplementedBoolOperators),
            Gen.Bool,
            (op, value) => new FilterSpec
            {
                Field = "IsActive",
                Operator = op,
                Value = value
            });

    private static Gen<FilterSpec> ValidFilterGen() =>
        Gen.OneOf<FilterSpec>(
            StringFieldFilterGen(),
            IntFieldFilterGen(),
            BoolFieldFilterGen());

    private static Gen<FilterSpec> UnhandledOperatorFilterGen() =>
        Gen.Select(
            Gen.OneOfConst(UnimplementedOperators),
            op => new FilterSpec
            {
                Field = "Name",
                Operator = op,
                Value = "test"
            });

    [Fact]
    public void ApplyFilter_OnValidFilter_NeverThrowsArgumentException()
    {
        Check.Sample(ValidFilterGen(), filter =>
        {
            using var ctx = new SampleContext(_options);
            try
            {
                _ = QuerySpecExpressionTranslator.ApplyFilter(ctx.Entities.AsQueryable(), filter).ToList();
            }
            catch (NotSupportedException) { }
            catch (InvalidCastException) { }
            catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
            {
                throw new Exception(
                    $"ApplyFilter threw {ex.GetType().Name} for operator {filter.Operator} on field '{filter.Field}': {ex.Message}", ex);
            }
        }, iter: 1000, threads: 1);
    }

    [Fact]
    public void ApplyFilter_OnUnhandledOperator_ThrowsNotSupportedException()
    {
        Check.Sample(UnhandledOperatorFilterGen(), filter =>
        {
            using var ctx = new SampleContext(_options);
            Assert.Throws<NotSupportedException>(() =>
                QuerySpecExpressionTranslator.ApplyFilter(ctx.Entities.AsQueryable(), filter).ToList());
        }, iter: 500, threads: 1);
    }
}
