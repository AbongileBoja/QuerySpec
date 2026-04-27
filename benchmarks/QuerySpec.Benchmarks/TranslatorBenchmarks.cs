using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;

namespace QuerySpec.Benchmarks;

/// <summary>
/// Measures the QuerySpecExpressionTranslator hot path: predicate construction, property
/// resolution (reflection cache), and nested filter composition.
/// </summary>
[Config(typeof(BenchConfig))]
public class TranslatorBenchmarks
{
    private IQueryable<Widget> _source = null!;
    private FilterSpec _simpleEqual = null!;
    private FilterSpec _compoundAnd = null!;
    private FilterSpec _deepNested = null!;
    private FilterSpec _inLarge = null!;

    private IQueryable<WidgetWithNullable> _nullableSource = null!;
    private FilterSpec _nullableGreaterThan = null!;
    private FilterSpec _nullableIsNull = null!;
    private FilterSpec _nullableBetween = null!;

    /// <summary>Seeds fixtures used by all benchmarks in this class.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _source = Enumerable.Range(0, 1000)
            .Select(i => new Widget { Id = i, Name = $"w{i}", Price = i * 1.5m, Category = i % 10 == 0 ? "A" : "B" })
            .AsQueryable();

        _simpleEqual = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A"
        };

        // Validate() requires Field on every node, so the outer carries the first predicate
        // and additional predicates live as children under its Logic.
        _compoundAnd = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
            Logic = LogicalOperator.And,
            Filters = new[]
            {
                new FilterSpec { Field = "Price", Operator = FilterOperator.GreaterThan, Value = 100m }
            }
        };

        _deepNested = new FilterSpec
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A",
            Logic = LogicalOperator.Or,
            Filters = new[]
            {
                new FilterSpec
                {
                    Field = "Category", Operator = FilterOperator.Equal, Value = "A",
                    Logic = LogicalOperator.And,
                    Filters = new[]
                    {
                        new FilterSpec { Field = "Price", Operator = FilterOperator.LessThan, Value = 50m }
                    }
                },
                new FilterSpec
                {
                    Field = "Name", Operator = FilterOperator.StartsWith, Value = "w9",
                    Logic = LogicalOperator.And,
                    Filters = new[]
                    {
                        new FilterSpec { Field = "Id", Operator = FilterOperator.GreaterThan, Value = 500 }
                    }
                }
            }
        };

        _inLarge = new FilterSpec
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = Enumerable.Range(0, 100).Select(i => (object)i).ToArray()
        };

        _nullableSource = Enumerable.Range(0, 1000)
            .Select(i => new WidgetWithNullable
            {
                Id = i,
                Name = $"w{i}",
                OptionalAge = i % 3 == 0 ? null : (int?)i,
                RegisteredAt = i % 5 == 0 ? null : (DateTime?)DateTime.UtcNow.AddDays(-i)
            })
            .AsQueryable();

        _nullableGreaterThan = new FilterSpec
        {
            Field = "OptionalAge",
            Operator = FilterOperator.GreaterThan,
            Value = 30
        };

        _nullableIsNull = new FilterSpec
        {
            Field = "OptionalAge",
            Operator = FilterOperator.IsNull
        };

        _nullableBetween = new FilterSpec
        {
            Field = "OptionalAge",
            Operator = FilterOperator.Between,
            Value = 10,
            ValueTo = 50
        };
    }

    /// <summary>Simplest predicate: single equality on a string column.</summary>
    [Benchmark(Baseline = true)]
    public int SimpleEqual()
        => QuerySpecExpressionTranslator.ApplyFilter(_source, _simpleEqual).Count();

    /// <summary>Two-way AND, string eq + decimal compare.</summary>
    [Benchmark]
    public int CompoundAnd()
        => QuerySpecExpressionTranslator.ApplyFilter(_source, _compoundAnd).Count();

    /// <summary>Two-level nested OR of ANDs with four operators.</summary>
    [Benchmark]
    public int DeepNested()
        => QuerySpecExpressionTranslator.ApplyFilter(_source, _deepNested).Count();

    /// <summary>IN operator with 100 values — measures array constant construction.</summary>
    [Benchmark]
    public int InLarge()
        => QuerySpecExpressionTranslator.ApplyFilter(_source, _inLarge).Count();

    /// <summary>
    /// Same workload as <see cref="CompoundAnd"/> but via the cached path. Measures the
    /// win from memoizing the compiled <c>Expression&lt;Func&lt;T,bool&gt;&gt;</c> for repeated
    /// filter shapes — the common case for real API workloads.
    /// </summary>
    [Benchmark]
    public int CompoundAnd_Cached()
        => QuerySpecExpressionTranslator.ApplyFilterCached(_source, _compoundAnd).Count();

    /// <summary>Deep-nested expression via the cached path.</summary>
    [Benchmark]
    public int DeepNested_Cached()
        => QuerySpecExpressionTranslator.ApplyFilterCached(_source, _deepNested).Count();

    /// <summary>
    /// Nullable int property with GreaterThan — exercises the HasValue + Value path in
    /// BuildComparison. On a warm cache this must show zero allocations in the inner loop.
    /// </summary>
    [Benchmark]
    public int NullableGreaterThan()
        => QuerySpecExpressionTranslator.ApplyFilter(_nullableSource, _nullableGreaterThan).Count();

    /// <summary>IsNull on a nullable int — exercises BuildIsNull HasValue path.</summary>
    [Benchmark]
    public int NullableIsNull()
        => QuerySpecExpressionTranslator.ApplyFilter(_nullableSource, _nullableIsNull).Count();

    /// <summary>Between on a nullable int — exercises BuildBetween HasValue + Value path.</summary>
    [Benchmark]
    public int NullableBetween()
        => QuerySpecExpressionTranslator.ApplyFilter(_nullableSource, _nullableBetween).Count();

    /// <summary>
    /// NullableGreaterThan via the cached predicate path — the steady-state case for real
    /// API workloads. Inner loop should show zero allocations.
    /// </summary>
    [Benchmark]
    public int NullableGreaterThan_Cached()
        => QuerySpecExpressionTranslator.ApplyFilterCached(_nullableSource, _nullableGreaterThan).Count();

    /// <summary>Simple entity used for translator benchmarks.</summary>
    public sealed class Widget
    {
        /// <summary>Identifier.</summary>
        public int Id { get; set; }
        /// <summary>Display name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Price.</summary>
        public decimal Price { get; set; }
        /// <summary>Category bucket.</summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>Entity with nullable value-type properties for nullable-operator benchmarks.</summary>
    public sealed class WidgetWithNullable
    {
        /// <summary>Identifier.</summary>
        public int Id { get; set; }
        /// <summary>Display name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Optional age — nullable int, exercises HasValue/Value cache paths.</summary>
        public int? OptionalAge { get; set; }
        /// <summary>Optional registration date — nullable DateTime, exercises date operator paths.</summary>
        public DateTime? RegisteredAt { get; set; }
    }
}
