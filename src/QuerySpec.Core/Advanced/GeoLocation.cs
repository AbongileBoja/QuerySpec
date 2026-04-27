using System;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Geospatial location for distance-based queries using Haversine formula.
/// </summary>
public class GeoLocation
{
    /// <summary>Latitude coordinate in decimal degrees.</summary>
    /// <remarks>
    /// Typed as <see cref="decimal"/> for historical reasons. The Haversine implementation casts
    /// to <see cref="double"/>, every JSON serializer maps to <see cref="double"/>, and SQL
    /// <c>geography</c> / <c>float8</c> use <see cref="double"/>. In 3.0 this property will change
    /// type from <see cref="decimal"/> to <see cref="double"/>; the change cannot be staged
    /// source-compatibly. Tracked in <see href="https://github.com/AbongileBoja/QuerySpec/issues/84">#84</see>.
    /// </remarks>
    public decimal Latitude { get; set; }
    /// <summary>Longitude coordinate in decimal degrees.</summary>
    /// <remarks>
    /// Typed as <see cref="decimal"/> for historical reasons. The Haversine implementation casts
    /// to <see cref="double"/>, every JSON serializer maps to <see cref="double"/>, and SQL
    /// <c>geography</c> / <c>float8</c> use <see cref="double"/>. In 3.0 this property will change
    /// type from <see cref="decimal"/> to <see cref="double"/>; the change cannot be staged
    /// source-compatibly. Tracked in <see href="https://github.com/AbongileBoja/QuerySpec/issues/84">#84</see>.
    /// </remarks>
    public decimal Longitude { get; set; }

    /// <summary>Initializes a new geolocation.</summary>
    public GeoLocation() { }
    /// <summary>Initializes a new geolocation with coordinates.</summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    public GeoLocation(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Calculates distance to another location in kilometers using the Haversine formula.
    /// </summary>
    /// <param name="other">The other location to measure to.</param>
    /// <returns>Great-circle distance in kilometres.</returns>
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
