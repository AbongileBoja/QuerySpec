// QSPEC0002 migration sample: AdvancedFilterExpression (mutable POCO) -> FilterSpec
// (immutable record with init accessors and value equality).

using QuerySpec.Core.Advanced;

// Old shape - mutable POCO, requires defensive copying for safe sharing across threads.
#pragma warning disable QSPEC0002
var legacy = new AdvancedFilterExpression
{
    Field = "Status",
    Operator = FilterOperator.Equal,
    Value = "Active",
    Logic = LogicalOperator.And,
    Filters = new List<AdvancedFilterExpression>
    {
        new() { Field = "CreatedAt", Operator = FilterOperator.GreaterThan, Value = DateTime.UtcNow.AddDays(-30) }
    }
};
Console.WriteLine($"[old] hash = {legacy.ComputeStableHash():X16}");
#pragma warning restore QSPEC0002

// Migration via FilterSpec.FromMutable - lossless projection.
var spec = FilterSpec.FromMutable(legacy);
Console.WriteLine($"[migrated] hash = {spec.ComputeStableHash():X16} (matches: {spec.ComputeStableHash() == legacy.ComputeStableHash()})");

// New shape - immutable construction with init.
var fresh = new FilterSpec
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

// Structural value equality: identical trees compare equal regardless of reference identity.
Console.WriteLine($"[new] structural equality: {spec == fresh || spec.Equals(fresh)} (note: timestamps differ, so this prints False unless seeded identically)");

// Round-trip back to legacy when interop is required.
var roundTrip = fresh.ToMutable();
Console.WriteLine($"[round-trip] field = {roundTrip.Field}, operator = {roundTrip.Operator}, child count = {roundTrip.Filters?.Count ?? 0}");

// Validation matches the legacy semantics.
var invalid = new FilterSpec { Field = "" };
var errors = string.Join(", ", invalid.Validate());
Console.WriteLine($"[new] validation: {errors}");
