# QSPEC0001: Use GeoCoordinate instead of GeoLocation.Latitude / .Longitude

| Property                       | Value                                                                                  |
| ------------------------------ | -------------------------------------------------------------------------------------- |
| Diagnostic ID                  | `QSPEC0001`                                                                            |
| Severity                       | Warning                                                                                |
| Introduced                     | QuerySpec 3.1                                                                          |
| Removal                        | QuerySpec 4.0                                                                          |
| Deprecated API                 | `QuerySpec.Core.Advanced.GeoLocation.Latitude` (`decimal`), `GeoLocation.Longitude`    |
| Replacement                    | `QuerySpec.Core.Advanced.GeoCoordinate` (`readonly record struct`, `double` lat/long)  |

## Cause

A program references `GeoLocation.Latitude` or `GeoLocation.Longitude`. Both properties are `decimal`, which contradicts every spatial library on .NET (NetTopologySuite, EF Core spatial mapping, SQL `geography` / `float8`) and is immediately cast to `double` by the Haversine implementation itself. The Framework Design Guidelines also recommend immutable types for value-like geographic data.

## Replacement

```csharp
// Before
var legacy = new GeoLocation(40.7128m, -74.0060m);
var distance = legacy.DistanceTo(other);

// After
var ny = new GeoCoordinate(40.7128, -74.0060);
var distance = ny.DistanceTo(other);

// Migration helper - allocation-free, intent-revealing.
var migrated = legacy.ToGeoCoordinate();
```

`GeoCoordinate` is a `readonly record struct` so it is naturally immutable, value-equal, and zero-allocation. It validates components at construction (`[-90, 90]` and `[-180, 180]`, no `NaN`) and supports ISO 6709 round-trip via `ToString` / `Parse` / `TryParse`. An `explicit` operator from `GeoLocation` is provided for one-off conversions.

## Suppression

```csharp
#pragma warning disable QSPEC0001
var lat = legacy.Latitude;
#pragma warning restore QSPEC0001
```

Or per-project (e.g. while migrating tests): add `<WarningsNotAsErrors>$(WarningsNotAsErrors);QSPEC0001</WarningsNotAsErrors>` to the relevant `Directory.Build.props`.

## See also

- Migration sample: `samples/Migration/GeoCoordinateMigration/Program.cs`
- Tracking issue: [#84](https://github.com/AbongileBoja/QuerySpec/issues/84)
