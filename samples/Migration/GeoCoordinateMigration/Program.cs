// QSPEC0001: GeoCoordinate replaces the 3.x GeoLocation type. The legacy decimal-based
// GeoLocation was removed in 4.0; the QuerySpec.Analyzers package emits QSPEC0001 against any
// remaining 3.x source so callers see the migration target before they upgrade.

using QuerySpec.Core.Advanced;

var ny = new GeoCoordinate(40.7128, -74.0060);
var la = new GeoCoordinate(34.0522, -118.2437);
Console.WriteLine($"NY -> LA = {ny.DistanceTo(la):F1} km");

var nyAlso = new GeoCoordinate(40.7128, -74.0060);
Console.WriteLine($"value equality: NY == NY' is {ny == nyAlso}");

var nyText = ny.ToString();
var nyParsed = GeoCoordinate.Parse(nyText);
Console.WriteLine($"{nyText} round-trips to {nyParsed} (equal: {ny == nyParsed})");

try
{
    _ = new GeoCoordinate(91.0, 0.0);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"validation: {ex.ParamName} -> {ex.Message.Split('.')[0]}");
}
