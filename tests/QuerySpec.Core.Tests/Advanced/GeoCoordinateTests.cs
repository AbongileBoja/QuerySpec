using System;
using QuerySpec.Core.Advanced;
using Xunit;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for <see cref="GeoCoordinate"/>.
/// </summary>
public class GeoCoordinateTests
{
    [Fact]
    public void Constructor_RejectsLatitudeAbove90()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(90.0001, 0));
    }

    [Fact]
    public void Constructor_RejectsLatitudeBelowMinus90()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(-90.0001, 0));
    }

    [Fact]
    public void Constructor_RejectsLongitudeAbove180()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(0, 180.0001));
    }

    [Fact]
    public void Constructor_RejectsLongitudeBelowMinus180()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(0, -180.0001));
    }

    [Fact]
    public void Constructor_RejectsNaNLatitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(double.NaN, 0));
    }

    [Fact]
    public void Constructor_RejectsNaNLongitude()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(0, double.NaN));
    }

    [Fact]
    public void Constructor_AcceptsBoundaryValues()
    {
        _ = new GeoCoordinate(90, 180);
        _ = new GeoCoordinate(-90, -180);
        _ = new GeoCoordinate(0, 0);
    }

    [Fact]
    public void DistanceTo_NewYorkToLosAngeles_IsApproximately3940Km()
    {
        var ny = new GeoCoordinate(40.7128, -74.0060);
        var la = new GeoCoordinate(34.0522, -118.2437);

        var distance = ny.DistanceTo(la);

        Assert.InRange(distance, 3930, 3960);
    }

    [Fact]
    public void DistanceTo_LondonToParis_IsApproximately344Km()
    {
        var london = new GeoCoordinate(51.5074, -0.1278);
        var paris = new GeoCoordinate(48.8566, 2.3522);

        var distance = london.DistanceTo(paris);

        Assert.InRange(distance, 330, 360);
    }

    [Fact]
    public void DistanceTo_SamePoint_IsZero()
    {
        var p = new GeoCoordinate(40.7128, -74.0060);
        Assert.Equal(0, p.DistanceTo(p));
    }

    [Fact]
    public void DistanceTo_AntipodalPoints_IsApproximately20015Km()
    {
        var north = new GeoCoordinate(90, 0);
        var south = new GeoCoordinate(-90, 0);
        Assert.InRange(north.DistanceTo(south), 19000, 21000);
    }

    [Fact]
    public void ToString_RoundTripsViaParse()
    {
        var original = new GeoCoordinate(40.712800, -74.006000);
        var rendered = original.ToString();
        var parsed = GeoCoordinate.Parse(rendered);

        Assert.Equal(original.Latitude, parsed.Latitude, 6);
        Assert.Equal(original.Longitude, parsed.Longitude, 6);
    }

    [Fact]
    public void Parse_RejectsMalformedString()
    {
        Assert.Throws<FormatException>(() => GeoCoordinate.Parse("not a coordinate"));
    }

    [Fact]
    public void Parse_NullThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GeoCoordinate.Parse(null!));
    }

    [Fact]
    public void TryParse_ReturnsFalseForNull()
    {
        Assert.False(GeoCoordinate.TryParse(null, out var result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryParse_ReturnsFalseForGarbage()
    {
        Assert.False(GeoCoordinate.TryParse("xyz", out _));
    }

    [Fact]
    public void TryParse_HandlesWellFormedInput()
    {
        Assert.True(GeoCoordinate.TryParse("+40.712800-074.006000/", out var result));
        Assert.Equal(40.7128, result.Latitude, 4);
        Assert.Equal(-74.006, result.Longitude, 4);
    }

    [Fact]
    public void Equality_StructuralValueEquality()
    {
        var a = new GeoCoordinate(40.7128, -74.0060);
        var b = new GeoCoordinate(40.7128, -74.0060);
        var c = new GeoCoordinate(40.7129, -74.0060);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }
}
