using System;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Geospatial location for distance-based queries using Haversine formula.
/// </summary>
/// <remarks>
/// Replaced by <see cref="GeoCoordinate"/>: an immutable <see langword="readonly"/>
/// <see langword="record"/> <see langword="struct"/> with <see cref="double"/> components, ISO 6709
/// round-trip, and validated construction. Call <see cref="ToGeoCoordinate"/> to migrate. The
/// individual <c>decimal</c> properties are obsolete (diagnostic id <c>QSPEC0001</c>) and will be
/// removed in 4.0; the type itself remains for binary compatibility through the 3.x line.
/// Tracked in <see href="https://github.com/AbongileBoja/QuerySpec/issues/84">#84</see>.
/// </remarks>
public class GeoLocation
{
    /// <summary>Latitude coordinate in decimal degrees.</summary>
    [Obsolete("Use GeoCoordinate.Latitude. The decimal lat/long types will be removed in 4.0. See QSPEC0001.",
        error: false,
        DiagnosticId = "QSPEC0001",
        UrlFormat = "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/{0}.md")]
    public decimal Latitude { get; set; }

    /// <summary>Longitude coordinate in decimal degrees.</summary>
    [Obsolete("Use GeoCoordinate.Longitude. The decimal lat/long types will be removed in 4.0. See QSPEC0001.",
        error: false,
        DiagnosticId = "QSPEC0001",
        UrlFormat = "https://github.com/AbongileBoja/QuerySpec/blob/main/docs/diagnostics/{0}.md")]
    public decimal Longitude { get; set; }

    /// <summary>Initializes a new geolocation.</summary>
    public GeoLocation() { }

    /// <summary>Initializes a new geolocation with coordinates.</summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    public GeoLocation(decimal latitude, decimal longitude)
    {
#pragma warning disable QSPEC0001 // setting on the obsolete properties is the constructor's intent
        Latitude = latitude;
        Longitude = longitude;
#pragma warning restore QSPEC0001
    }

    /// <summary>
    /// Calculates distance to another location in kilometers using the Haversine formula.
    /// </summary>
    /// <param name="other">The other location to measure to.</param>
    /// <returns>Great-circle distance in kilometres.</returns>
    public double DistanceTo(GeoLocation other)
    {
        const double R = 6371; // Earth radius in km
#pragma warning disable QSPEC0001 // intra-type read of the obsolete properties is intended
        var lat1 = (double)Latitude;
        var lat2 = (double)other.Latitude;
        var lon1 = (double)Longitude;
        var lon2 = (double)other.Longitude;
#pragma warning restore QSPEC0001

        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    /// <summary>
    /// Converts this legacy <see cref="GeoLocation"/> to the new immutable
    /// <see cref="GeoCoordinate"/>. This is the recommended migration path off the obsolete
    /// <see cref="Latitude"/> / <see cref="Longitude"/> properties.
    /// </summary>
    /// <returns>An equivalent <see cref="GeoCoordinate"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when this geolocation's components are out of valid range.</exception>
    public GeoCoordinate ToGeoCoordinate()
    {
#pragma warning disable QSPEC0001 // migration helper reads the obsolete properties intentionally
        return new GeoCoordinate((double)Latitude, (double)Longitude);
#pragma warning restore QSPEC0001
    }
}
