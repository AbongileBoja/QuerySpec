using System;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Geospatial location for distance-based queries using Haversine formula.
/// </summary>
public class GeoLocation
{
    /// <summary>Latitude coordinate.</summary>
    public decimal Latitude { get; set; }
    /// <summary>Longitude coordinate.</summary>
    public decimal Longitude { get; set; }

    /// <summary>Initializes a new geolocation.</summary>
    public GeoLocation() { }
    /// <summary>Initializes a new geolocation with coordinates.</summary>
    public GeoLocation(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Calculates distance to another location in kilometers.
    /// </summary>
    public double DistanceTo(GeoLocation other)
    {
        const double R = 6371; // Earth radius in km
        var lat1 = (double)Latitude;
        var lat2 = (double)other.Latitude;
        var lon1 = (double)Longitude;
        var lon2 = (double)other.Longitude;

        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
