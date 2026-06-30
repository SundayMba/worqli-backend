using Servika.Application.Abstractions.Directions;
using Servika.Contracts.Tracking;

namespace Servika.Application.Tracking;

/// <summary>
/// Returns a driving route + ETA between two points for the tracking map. Thin
/// wrapper over <see cref="IDirectionsProvider"/> that validates the coordinates
/// and maps the provider result to the public DTO.
/// </summary>
public sealed class GetRouteHandler
{
    private readonly IDirectionsProvider _directions;

    public GetRouteHandler(IDirectionsProvider directions)
    {
        _directions = directions;
    }

    public async Task<RouteResponse> HandleAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        Validate(fromLat, fromLng, nameof(fromLat));
        Validate(toLat, toLng, nameof(toLat));

        var result = await _directions.GetRouteAsync(fromLat, fromLng, toLat, toLng, ct);

        return new RouteResponse(
            result.Points.Select(p => new GeoPoint(p.Latitude, p.Longitude)).ToList(),
            result.DistanceMeters,
            result.DurationSeconds,
            result.Provider);
    }

    private static void Validate(double lat, double lng, string paramName)
    {
        if (lat is < -90 or > 90)
            throw new ArgumentException("Latitude must be between -90 and 90.", paramName);
        if (lng is < -180 or > 180)
            throw new ArgumentException("Longitude must be between -180 and 180.", paramName);
    }
}
