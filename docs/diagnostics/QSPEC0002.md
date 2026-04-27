# QSPEC0002: Use FilterSpec instead of AdvancedFilterExpression

| Property                       | Value                                                                              |
| ------------------------------ | ---------------------------------------------------------------------------------- |
| Diagnostic ID                  | `QSPEC0002`                                                                        |
| Severity                       | Warning                                                                            |
| Introduced                     | QuerySpec 3.1                                                                      |
| Removal                        | QuerySpec 4.0                                                                      |
| Deprecated API                 | `QuerySpec.Core.Advanced.AdvancedFilterExpression` (mutable POCO with `set;`)      |
| Replacement                    | `QuerySpec.Core.Advanced.FilterSpec` (`record` with `init` accessors, value equal) |

## Cause

A program references `AdvancedFilterExpression`. The mutable POCO shape leaks aliasing into compiled-expression caches and forces defensive copies for safe sharing. The `Validate()` return shape (`IEnumerable<string>`) does not align with the BCL `IValidatableObject` / `ValidationException` pattern. Filter graphs are read far more often than they are written, so an immutable record is the ergonomic primitive.

## Replacement

```csharp
// Before
var legacy = new AdvancedFilterExpression
{
    Field = "Status",
    Operator = FilterOperator.Equal,
    Value = "Active",
};

// After
var spec = new FilterSpec
{
    Field = "Status",
    Operator = FilterOperator.Equal,
    Value = "Active",
};

// Round-trip helpers when crossing the deprecation window.
FilterSpec fromLegacy = FilterSpec.FromMutable(legacy);
AdvancedFilterExpression toLegacy = spec.ToMutable();

// EF Core translator overload (preferred for new code).
var query = QuerySpecExpressionTranslator.ApplyFilter(source, spec);
```

`FilterSpec` is a `sealed record` with `init` accessors, structural value equality across child trees, and `ComputeStableHash()` whose semantics match the legacy implementation so cached predicate keys round-trip across the deprecation window.

## Suppression

```csharp
#pragma warning disable QSPEC0002
var legacy = new AdvancedFilterExpression { /* ... */ };
#pragma warning restore QSPEC0002
```

## See also

- Migration sample: `samples/Migration/FilterSpecMigration/Program.cs`
- Tracking issue: [#84](https://github.com/AbongileBoja/QuerySpec/issues/84)
