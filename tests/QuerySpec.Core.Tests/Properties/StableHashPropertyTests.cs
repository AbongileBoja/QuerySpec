using System;
using System.Collections.Generic;
using CsCheck;
using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Properties;

[Trait("Category", "PropertyBased")]
public sealed class StableHashPropertyTests
{
    private static readonly FilterOperator[] LeafOperators =
    [
        FilterOperator.Equal, FilterOperator.NotEqual,
        FilterOperator.GreaterThan, FilterOperator.LessThan,
        FilterOperator.Contains, FilterOperator.StartsWith, FilterOperator.EndsWith,
        FilterOperator.IsNull, FilterOperator.IsNotNull,
        FilterOperator.IsEmpty, FilterOperator.IsNotEmpty
    ];

    private static readonly string[] Fields = ["Name", "Age", "IsActive", "Score", "CreatedAt"];

    private static Gen<AdvancedFilterExpression> LeafFilterGen() =>
        Gen.Select(
            Gen.OneOfConst(Fields),
            Gen.OneOfConst(LeafOperators),
            Gen.String,
            (field, op, value) => new AdvancedFilterExpression
            {
                Field = field,
                Operator = op,
                Value = value
            });

    private static Gen<AdvancedFilterExpression> ComposedFilterGen(int maxDepth = 8)
    {
        return Gen.Recursive<AdvancedFilterExpression>((depth, inner) =>
        {
            if (depth >= maxDepth)
                return LeafFilterGen();

            return Gen.Frequency(
                (3, LeafFilterGen()),
                (1, Gen.Select(
                    inner,
                    inner,
                    Gen.OneOfConst(new[] { LogicalOperator.And, LogicalOperator.Or }),
                    (left, right, logic) => new AdvancedFilterExpression
                    {
                        Field = left.Field,
                        Operator = left.Operator,
                        Value = left.Value,
                        Logic = logic,
                        Filters = new System.Collections.Generic.List<AdvancedFilterExpression> { left, right }
                    })));
        });
    }

    [Fact]
    public void ComputeStableHash_StructurallyDistinctFilters_ProduceDistinctHashes()
    {
        const int sampleSize = 5000;
        const double minDistinctRatio = 0.95;

        var hashes = new System.Collections.Concurrent.ConcurrentBag<long>();

        Check.Sample(ComposedFilterGen(), filter =>
        {
            hashes.Add(filter.ComputeStableHash());
        }, iter: sampleSize, threads: 1);

        var total = hashes.Count;
        var distinct = new HashSet<long>(hashes).Count;
        var actualRatio = (double)distinct / total;
        Assert.True(actualRatio >= minDistinctRatio,
            $"Hash cardinality ratio {actualRatio:P1} is below minimum {minDistinctRatio:P1} ({distinct} distinct hashes from {total} samples). " +
            "This indicates a collision rate that is too high for a cache key and signals a bug in ComputeStableHash.");
    }
}
