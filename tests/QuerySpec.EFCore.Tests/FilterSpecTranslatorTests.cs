using System;
using System.Collections.Generic;
using System.Linq;
using QuerySpec.Core.Advanced;
using QuerySpec.EFCore;
using Xunit;

namespace QuerySpec.EFCore.Tests;

/// <summary>
/// Verifies <see cref="QuerySpecExpressionTranslator.ApplyFilter{T}(IQueryable{T}, FilterSpec?)"/>
/// produces the same result set as the legacy <see cref="AdvancedFilterExpression"/> overload for
/// equivalent filter trees through the deprecation window.
/// </summary>
public class FilterSpecTranslatorTests
{
    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private static IQueryable<Person> Seed() => new[]
    {
        new Person { Id = 1, Name = "Ada",  Age = 36, Status = "Active" },
        new Person { Id = 2, Name = "Bob",  Age = 28, Status = "Inactive" },
        new Person { Id = 3, Name = "Cleo", Age = 42, Status = "Active" },
        new Person { Id = 4, Name = "Dax",  Age = 19, Status = "Active" },
    }.AsQueryable();

    [Fact]
    public void Simple_FilterSpec_MatchesLegacyOverload()
    {
#pragma warning disable QSPEC0002
        var legacy = new AdvancedFilterExpression
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
        };
#pragma warning restore QSPEC0002
        var spec = FilterSpec.FromMutable(legacy);

#pragma warning disable QSPEC0002
        var legacyResult = QuerySpecExpressionTranslator.ApplyFilter(Seed(), legacy).Select(p => p.Id).ToArray();
#pragma warning restore QSPEC0002
        var specResult = QuerySpecExpressionTranslator.ApplyFilter(Seed(), spec).Select(p => p.Id).ToArray();

        Assert.Equal(legacyResult, specResult);
        Assert.Equal(new[] { 1, 3, 4 }, specResult);
    }

    [Fact]
    public void Composite_FilterSpec_MatchesLegacyOverload()
    {
#pragma warning disable QSPEC0002
        var legacy = new AdvancedFilterExpression
        {
            Field = "Status",
            Operator = FilterOperator.Equal,
            Value = "Active",
            Logic = LogicalOperator.And,
            Filters = new List<AdvancedFilterExpression>
            {
                new() { Field = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 30 },
            },
        };
#pragma warning restore QSPEC0002
        var spec = FilterSpec.FromMutable(legacy);

#pragma warning disable QSPEC0002
        var legacyResult = QuerySpecExpressionTranslator.ApplyFilter(Seed(), legacy).Select(p => p.Id).ToArray();
#pragma warning restore QSPEC0002
        var specResult = QuerySpecExpressionTranslator.ApplyFilter(Seed(), spec).Select(p => p.Id).ToArray();

        Assert.Equal(legacyResult, specResult);
        Assert.Equal(new[] { 1, 3 }, specResult);
    }

    [Fact]
    public void NullFilterSpec_ReturnsSourceUnchanged()
    {
        var source = Seed();
        var result = QuerySpecExpressionTranslator.ApplyFilter(source, (FilterSpec?)null);
        Assert.Same(source, result);
    }
}
