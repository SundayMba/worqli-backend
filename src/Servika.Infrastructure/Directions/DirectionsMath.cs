using Servika.Application.Abstractions.Directions;

namespace Servika.Infrastructure.Directions;

/// <summary>
/// Shared geo helpers for the directions providers: a haversine distance, a
/// straight-line fallback route, and a decoder for Google's encoded polyline
/// format.
/// </summary>
internal static class DirectionsMath
{
    private const double EarthRadiusM = 6_371_000;
    // City-traffic average for the stub ETA (Lagos ≈ 22 km/h → m/s).
    private const double AvgSpeedMetersPerSecond = 22_000.0 / 3600.0;

    public static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        double ToRad(double d) => d * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>A two-point straight line + haversine distance + average-speed ETA.</summary>
    public static RouteResult StraightLine(
        double fromLat, double fromLng, double toLat, double toLng, string provider)
    {
        var meters = HaversineMeters(fromLat, fromLng, toLat, toLng);
        var seconds = Math.Max(60, meters / AvgSpeedMetersPerSecond);
        return new RouteResult(
            new[] { new RoutePoint(fromLat, fromLng), new RoutePoint(toLat, toLng) },
            meters, seconds, provider);
    }

    /// <summary>Decodes a Google encoded polyline string into points.</summary>
    public static List<RoutePoint> DecodePolyline(string encoded)
    {
        var points = new List<RoutePoint>();
        int index = 0, lat = 0, lng = 0;

        while (index < encoded.Length)
        {
            lat += DecodeDelta(encoded, ref index);
            lng += DecodeDelta(encoded, ref index);
            points.Add(new RoutePoint(lat / 1e5, lng / 1e5));
        }
        return points;
    }

    private static int DecodeDelta(string encoded, ref int index)
    {
        int shift = 0, result = 0, b;
        do
        {
            b = encoded[index++] - 63;
            result |= (b & 0x1f) << shift;
            shift += 5;
        }
        while (b >= 0x20);
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
