# QSPEC0010: Use ContainsCaseInsensitive instead of Contains_CaseInsensitive

| Property        | Value                                                       |
| --------------- | ----------------------------------------------------------- |
| Diagnostic ID   | `QSPEC0010`                                                 |
| Severity        | Warning (`[Obsolete(error: false)]`)                        |
| Introduced      | QuerySpec 4.0.0                                             |
| Deprecated API  | `QuerySpec.Core.Advanced.FilterOperator.Contains_CaseInsensitive` |
| Replacement     | `QuerySpec.Core.Advanced.FilterOperator.ContainsCaseInsensitive`  |

## Cause

A program references the snake-case enum member `FilterOperator.Contains_CaseInsensitive`. The Framework Design Guidelines require enum members to use PascalCase (`Capitalization Conventions`). The replacement member `ContainsCaseInsensitive` shares the same underlying value (`52`), so existing serialized data and EF Core column mappings continue to round-trip without change.

## Replacement

```csharp
// Before
var spec = new FilterSpec
{
    Field = "Name",
    Operator = FilterOperator.Contains_CaseInsensitive,
    Value = "alice",
};

// After
var spec = new FilterSpec
{
    Field = "Name",
    Operator = FilterOperator.ContainsCaseInsensitive,
    Value = "alice",
};
```

Both members evaluate to the integer value `52`, so any database column or JSON document that stores the numeric form continues to deserialize correctly. Only the source-level identifier needs to change.

## Suppression

```csharp
#pragma warning disable QSPEC0010
var op = FilterOperator.Contains_CaseInsensitive;
#pragma warning restore QSPEC0010
```

Or per-project: add `<WarningsNotAsErrors>$(WarningsNotAsErrors);QSPEC0010</WarningsNotAsErrors>` to `Directory.Build.props`.

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
