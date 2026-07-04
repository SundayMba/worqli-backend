namespace Servika.Application.Catalogue;

/// <summary>
/// Great-circle distance between two lat/lng points (haversine), in kilometres.
/// The artisan set is small, so proximity is computed in-memory in the handler
/// rather than in SQL (no PostGIS dependency for the catalogue slice).
/// </summary>
internal static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double Km(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        return EarthRadiusKm * c;
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
