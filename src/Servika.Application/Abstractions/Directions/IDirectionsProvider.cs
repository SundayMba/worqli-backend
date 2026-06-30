namespace Servika.Application.Abstractions.Directions;

/// <summary>A point on a computed route.</summary>
public sealed record RoutePoint(double Latitude, double Longitude);

/// <summary>
/// A driving route: the polyline to draw, plus distance and (traffic-aware where
/// available) duration. <see cref="Provider"/> distinguishes real road geometry
/// ("google") from the straight-line stub ("stub").
/// </summary>
public sealed record RouteResult(
    IReadOnlyList<RoutePoint> Points,
    double DistanceMeters,
    double DurationSeconds,
    string Provider);

/// <summary>
/// Computes a driving route between two coordinates. Implemented by a real
/// directions service (Google) when configured, else a straight-line stub — same
/// port either way, so the use case never changes. The key lives server-side, so
/// the mobile app calls our API rather than the provider directly.
/// </summary>
public interface IDirectionsProvider
{
    Task<RouteResult> GetRouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
}
