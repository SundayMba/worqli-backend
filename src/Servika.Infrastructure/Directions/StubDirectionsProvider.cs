using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Directions;

namespace Servika.Infrastructure.Directions;

/// <summary>
/// Keyless fallback directions provider: a straight line between the two points,
/// haversine distance, and an average-speed ETA. Selected when no Google
/// Directions key is configured, so the tracking map works in local dev — the
/// client sees <c>provider: "stub"</c> and can render the line as approximate.
/// </summary>
public sealed class StubDirectionsProvider : IDirectionsProvider
{
    private readonly ILogger<StubDirectionsProvider> _logger;

    public StubDirectionsProvider(ILogger<StubDirectionsProvider> logger)
    {
        _logger = logger;
    }

    public Task<RouteResult> GetRouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        _logger.LogDebug("[STUB-ROUTE] straight line ({FromLat},{FromLng}) → ({ToLat},{ToLng})",
            fromLat, fromLng, toLat, toLng);
        return Task.FromResult(DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub"));
    }
}
