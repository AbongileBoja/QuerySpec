// QSPEC0001 migration sample: GeoLocation (decimal lat/long, mutable) -> GeoCoordinate
// (immutable readonly record struct with double components).
//
// Old usage emits warning QSPEC0001 in 3.x (warning-only; not a source break). The new path
// is allocation-free, value-equal, and offers ISO 6709 round-trip plus validated construction.

using QuerySpec.Core.Advanced;

// Old shape - silenced so this sample compiles cleanly without test-project warning relaxation.
#pragma warning disable QSPEC0001
var nyOld = new GeoLocation(40.7128m, -74.0060m);
var laOld = new GeoLocation(34.0522m, -118.2437m);
Console.WriteLine($"[old] NY -> LA = {nyOld.DistanceTo(laOld):F1} km, NY=({nyOld.Latitude},{nyOld.Longitude})");
#pragma warning restore QSPEC0001

// Migration helper - non-obsolete, recommended path.
#pragma warning disable QSPEC0001
var nyConverted = nyOld.ToGeoCoordinate();
#pragma warning restore QSPEC0001
Console.WriteLine($"[migrated] NY = {nyConverted}, lat={nyConverted.Latitude}, lon={nyConverted.Longitude}");

// New shape - direct construction.
var ny = new GeoCoordinate(40.7128, -74.0060);
var la = new GeoCoordinate(34.0522, -118.2437);
Console.WriteLine($"[new] NY -> LA = {ny.DistanceTo(la):F1} km");

// Value equality and ISO 6709 round-trip.
var nyAlso = new GeoCoordinate(40.7128, -74.0060);
Console.WriteLine($"[new] equality: NY == NY' is {ny == nyAlso}");

var nyText = ny.ToString();
var nyParsed = GeoCoordinate.Parse(nyText);
Console.WriteLine($"[new] {nyText} round-trips to {nyParsed} (equal: {ny == nyParsed})");

// Validation rejects out-of-range inputs at construction.
try
{
    _ = new GeoCoordinate(91.0, 0.0);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"[new] validation: {ex.ParamName} -> {ex.Message.Split('.')[0]}");
}
