// QSPEC0002: FilterSpec replaces the 3.x AdvancedFilterExpression POCO. The legacy mutable
// type was removed in 4.0; the QuerySpec.Analyzers package emits QSPEC0002 against any
// remaining 3.x source so callers see the migration target before they upgrade.

using QuerySpec.Core.Advanced;

var spec = new FilterSpec
{
    Field = "Status",
    Operator = FilterOperator.Equal,
    Value = "Active",
    Logic = LogicalOperator.And,
    Filters = new[]
    {
        new FilterSpec { Field = "CreatedAt", Operator = FilterOperator.GreaterThan, Value = DateTime.UtcNow.AddDays(-30) }
    }
};
Console.WriteLine($"hash = {spec.ComputeStableHash():X16}");

var clone = new FilterSpec
{
    Field = "Status",
    Operator = FilterOperator.Equal,
    Value = "Active",
    Logic = LogicalOperator.And,
    Filters = new[]
    {
        new FilterSpec { Field = "CreatedAt", Operator = FilterOperator.GreaterThan, Value = DateTime.UtcNow.AddDays(-30) }
    }
};
Console.WriteLine($"structural equality: {spec.Equals(clone)}");

var invalid = new FilterSpec { Field = "" };
var errors = string.Join(", ", invalid.Validate());
Console.WriteLine($"validation: {errors}");
