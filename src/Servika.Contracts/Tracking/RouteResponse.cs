namespace Servika.Contracts.Tracking;

/// <summary>A single point on a route (WGS-84).</summary>
public sealed record GeoPoint(double Latitude, double Longitude);

/// <summary>
/// A driving route between two points, returned by <c>GET /tracking/route</c>.
/// <paramref name="Points"/> is the road-snapped polyline to draw on the map (just
/// the two endpoints when the stub provider is used). <paramref name="DurationSeconds"/>
/// is the (traffic-aware, when available) ETA. <paramref name="Provider"/> is
/// "google" or "stub" so the client knows whether the line is real road geometry.
/// </summary>
public sealed record RouteResponse(
    IReadOnlyList<GeoPoint> Points,
    double DistanceMeters,
    double DurationSeconds,
    string Provider);
