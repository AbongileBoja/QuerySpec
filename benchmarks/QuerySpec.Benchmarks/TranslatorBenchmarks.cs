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
    private AdvancedFilterExpression _simpleEqual = null!;
    private AdvancedFilterExpression _compoundAnd = null!;
    private AdvancedFilterExpression _deepNested = null!;
    private AdvancedFilterExpression _inLarge = null!;

    /// <summary>Seeds fixtures used by all benchmarks in this class.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _source = Enumerable.Range(0, 1000)
            .Select(i => new Widget { Id = i, Name = $"w{i}", Price = i * 1.5m, Category = i % 10 == 0 ? "A" : "B" })
            .AsQueryable();

        _simpleEqual = new AdvancedFilterExpression
        {
            Field = "Category",
            Operator = FilterOperator.Equal,
            Value = "A"
        };

        // Validate() requires Field on every node, so the outer carries the first predicate
        // and additional predicates live as children under its Logic.
        _compoundAnd = new AdvancedFilterExpression
        {
            Field = "Category", Operator = FilterOperator.Equal, Value = "A",
            Logic = LogicalOperator.And,
            Filters = new()
            {
                new AdvancedFilterExpression { Field = "Price", Operator = FilterOperator.GreaterThan, Value = 100m }
            }
        };

        _deepNested = new AdvancedFilterExpression
        {
            Field = "Category", Operator = FilterOperator.Equal, Value = "A",
            Logic = LogicalOperator.Or,
            Filters = new()
            {
                new AdvancedFilterExpression
                {
                    Field = "Category", Operator = FilterOperator.Equal, Value = "A",
                    Logic = LogicalOperator.And,
                    Filters = new()
                    {
                        new AdvancedFilterExpression { Field = "Price", Operator = FilterOperator.LessThan, Value = 50m }
                    }
                },
                new AdvancedFilterExpression
                {
                    Field = "Name", Operator = FilterOperator.StartsWith, Value = "w9",
                    Logic = LogicalOperator.And,
                    Filters = new()
                    {
                        new AdvancedFilterExpression { Field = "Id", Operator = FilterOperator.GreaterThan, Value = 500 }
                    }
                }
            }
        };

        _inLarge = new AdvancedFilterExpression
        {
            Field = "Id",
            Operator = FilterOperator.In,
            Value = Enumerable.Range(0, 100).Select(i => (object)i).ToArray()
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
}
