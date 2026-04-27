using System;
using System.Globalization;

namespace QuerySpec.Core.Advanced;

/// <summary>
/// Immutable WGS-84 geographic coordinate (latitude/longitude as <see cref="double"/>) intended
/// to replace <see cref="GeoLocation"/> in the 4.0 line. Modeled as a <see langword="readonly"/>
/// <see langword="record"/> <see langword="struct"/> per Framework Design Guidelines: zero-allocation,
/// value-equality, and naturally thread-safe.
/// </summary>
/// <remarks>
/// Coordinates are validated at construction. Latitude must be in [-90, 90]; longitude must be in
/// [-180, 180]; <see cref="double.NaN"/> is rejected for both. The <see cref="GeoLocation"/> -&gt;
/// <see cref="GeoCoordinate"/> conversion is intentionally <see langword="explicit"/> because it
/// narrows precision (decimal -&gt; double); call <see cref="GeoLocation.ToGeoCoordinate"/> for an
/// allocation-free intent-revealing form. <see cref="ToString"/> emits ISO 6709 H-style; round-trip
/// via <see cref="Parse(string)"/> / <see cref="TryParse(string, out GeoCoordinate)"/>.
/// </remarks>
public readonly record struct GeoCoordinate
{
    /// <summary>Latitude in decimal degrees, in the closed range [-90, 90].</summary>
    public double Latitude { get; }

    /// <summary>Longitude in decimal degrees, in the closed range [-180, 180].</summary>
    public double Longitude { get; }

    /// <summary>Initializes a new <see cref="GeoCoordinate"/>.</summary>
    /// <param name="latitude">Latitude in decimal degrees; must be in [-90, 90] and not <see cref="double.NaN"/>.</param>
    /// <param name="longitude">Longitude in decimal degrees; must be in [-180, 180] and not <see cref="double.NaN"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="latitude"/> or <paramref name="longitude"/> is outside its valid range or is <see cref="double.NaN"/>.</exception>
    public GeoCoordinate(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude < -90d || latitude > 90d)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be in [-90, 90].");
        if (double.IsNaN(longitude) || longitude < -180d || longitude > 180d)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be in [-180, 180].");
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Returns the great-circle distance from this coordinate to <paramref name="other"/> in
    /// kilometres using the Haversine formula on a spherical-earth model (radius 6371 km).
    /// </summary>
    /// <param name="other">The destination coordinate.</param>
    /// <returns>Distance in kilometres; never negative.</returns>
    public double DistanceTo(GeoCoordinate other)
    {
        const double R = 6371d;
        var dLat = (other.Latitude - Latitude) * Math.PI / 180d;
        var dLon = (other.Longitude - Longitude) * Math.PI / 180d;
        var lat1 = Latitude * Math.PI / 180d;
        var lat2 = other.Latitude * Math.PI / 180d;

        var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d)
                + Math.Cos(lat1) * Math.Cos(lat2)
                  * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return R * c;
    }

    /// <summary>
    /// Renders the coordinate in ISO 6709 H-style with signed fixed-precision components and a
    /// trailing solidus, e.g. <c>+12.345000-067.890000/</c>. Precision is six decimal places —
    /// roughly 11 cm at the equator — chosen so round-trip via <see cref="Parse(string)"/>
    /// preserves the value to the same precision.
    /// </summary>
    /// <returns>An ISO 6709 H-style string suitable for transport and round-tripping.</returns>
    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1:00.000000}{2}{3:000.000000}/",
            Latitude >= 0 ? "+" : "-", Math.Abs(Latitude),
            Longitude >= 0 ? "+" : "-", Math.Abs(Longitude));
    }

    /// <summary>
    /// Parses an ISO 6709 H-style coordinate string of the form <c>±DD.DDDDDD±DDD.DDDDDD/</c>.
    /// </summary>
    /// <param name="s">The string to parse. Must not be null.</param>
    /// <returns>The parsed <see cref="GeoCoordinate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid ISO 6709 H-style coordinate.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the parsed components are out of range.</exception>
    public static GeoCoordinate Parse(string s)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));
        if (!TryParseCore(s, out var lat, out var lon))
            throw new FormatException($"'{s}' is not a valid ISO 6709 coordinate.");
        return new GeoCoordinate(lat, lon);
    }

    /// <summary>
    /// Attempts to parse an ISO 6709 H-style coordinate string. Returns <see langword="false"/>
    /// without throwing on any parse, range, or null-input failure.
    /// </summary>
    /// <param name="s">The string to parse. May be null.</param>
    /// <param name="result">The parsed coordinate on success; <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? s, out GeoCoordinate result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        if (!TryParseCore(s, out var lat, out var lon))
        {
            result = default;
            return false;
        }
        if (double.IsNaN(lat) || lat < -90d || lat > 90d
            || double.IsNaN(lon) || lon < -180d || lon > 180d)
        {
            result = default;
            return false;
        }
        result = new GeoCoordinate(lat, lon);
        return true;
    }

    private static bool TryParseCore(string s, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (string.IsNullOrEmpty(s)) return false;

        var trimmed = s.EndsWith('/') ? s.AsSpan(0, s.Length - 1) : s.AsSpan();
        if (trimmed.Length < 4) return false;

        // Latitude sign must be at index 0; longitude sign is the next '+' or '-' after index 0.
        if (trimmed[0] != '+' && trimmed[0] != '-') return false;

        var sepIdx = -1;
        for (var i = 1; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '+' || trimmed[i] == '-')
            {
                sepIdx = i;
                break;
            }
        }
        if (sepIdx <= 0) return false;

        var latSpan = trimmed[..sepIdx];
        var lonSpan = trimmed[sepIdx..];

        return double.TryParse(latSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
               && double.TryParse(lonSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
    }

    /// <summary>
    /// Narrowing conversion from the legacy <see cref="GeoLocation"/> (decimal lat/long) to a
    /// <see cref="GeoCoordinate"/> (double lat/long). Marked <see langword="explicit"/> because
    /// the cast loses precision and may throw <see cref="ArgumentOutOfRangeException"/> if the
    /// legacy values are out of range.
    /// </summary>
    /// <param name="legacy">The legacy geolocation. Must not be null.</param>
    /// <returns>The equivalent <see cref="GeoCoordinate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="legacy"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the legacy components are out of valid range.</exception>
    public static explicit operator GeoCoordinate(GeoLocation legacy)
    {
        if (legacy is null) throw new ArgumentNullException(nameof(legacy));
#pragma warning disable QSPEC0001 // intentional: this is the migration path
        return new GeoCoordinate((double)legacy.Latitude, (double)legacy.Longitude);
#pragma warning restore QSPEC0001
    }
}
