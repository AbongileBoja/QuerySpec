using Xunit;
using QuerySpec.Core.Advanced;

namespace QuerySpec.Core.Tests.Advanced;

/// <summary>
/// Unit tests for GeoLocation.
/// </summary>
public class GeoLocationTests
{
    /// <summary>Tests that DistanceTo calculates correct distance between locations. To the moon and back.</summary>
    [Fact]
    public void DistanceTo_Should_Calculate_Correct_Distance()
    {
        // Arrange
        var loc1 = new GeoLocation(40.7128m, -74.0060m); // New York
        var loc2 = new GeoLocation(34.0522m, -118.2437m); // Los Angeles

        // Act
        var distance = loc1.DistanceTo(loc2);

        // Assert
        Assert.InRange(distance, 3900, 4000); // ~3940 km
    }

    /// <summary>Tests that DistanceTo returns zero for the same location.</summary>
    [Fact]
    public void DistanceTo_Should_Be_Zero_For_Same_Location()
    {
        // Arrange
        var loc1 = new GeoLocation(40.7128m, -74.0060m);
        var loc2 = new GeoLocation(40.7128m, -74.0060m);

        // Act
        var distance = loc1.DistanceTo(loc2);

        // Assert
        Assert.Equal(0, distance);
    }

    /// <summary>Tests that Constructor sets latitude and longitude coordinates.</summary>
    [Fact]
    public void Constructor_Should_Set_Coordinates()
    {
        // Arrange & Act
        var loc = new GeoLocation(40.7128m, -74.0060m);

        // Assert
        Assert.Equal(40.7128m, loc.Latitude);
        Assert.Equal(-74.0060m, loc.Longitude);
    }
}
