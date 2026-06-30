using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Directions;

namespace Servika.Infrastructure.Directions;

/// <summary>
/// Google Directions API implementation of <see cref="IDirectionsProvider"/>.
/// Requests a driving route (traffic-aware via <c>departure_time=now</c>), decodes
/// the overview polyline, and returns distance + duration. The API key is held
/// server-side (config), so it never ships in the mobile app. Any failure or
/// empty result falls back to a straight line so the map always has something to
/// draw. Selected by DI only when a Directions key is configured.
/// </summary>
public sealed class GoogleDirectionsProvider : IDirectionsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleDirectionsOptions _options;
    private readonly ILogger<GoogleDirectionsProvider> _logger;

    public GoogleDirectionsProvider(
        IHttpClientFactory httpClientFactory,
        GoogleDirectionsOptions options,
        ILogger<GoogleDirectionsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<RouteResult> GetRouteAsync(
        double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("google-directions");
            var inv = CultureInfo.InvariantCulture;
            var url =
                $"{_options.DirectionsBaseUrl}/maps/api/directions/json" +
                $"?origin={fromLat.ToString(inv)},{fromLng.ToString(inv)}" +
                $"&destination={toLat.ToString(inv)},{toLng.ToString(inv)}" +
                $"&mode=driving&departure_time=now&key={_options.DirectionsApiKey}";

            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var status = root.GetProperty("status").GetString();
            if (status != "OK" || !root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                _logger.LogWarning("Google Directions returned '{Status}' — falling back to straight line.", status);
                return DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub");
            }

            var route = routes[0];
            var encoded = route.GetProperty("overview_polyline").GetProperty("points").GetString() ?? "";
            var leg = route.GetProperty("legs")[0];
            var distanceMeters = leg.GetProperty("distance").GetProperty("value").GetDouble();
            // Prefer traffic-aware duration when present.
            var durationSeconds = leg.TryGetProperty("duration_in_traffic", out var dit)
                ? dit.GetProperty("value").GetDouble()
                : leg.GetProperty("duration").GetProperty("value").GetDouble();

            var points = DirectionsMath.DecodePolyline(encoded);
            if (points.Count == 0)
                points = [new RoutePoint(fromLat, fromLng), new RoutePoint(toLat, toLng)];

            return new RouteResult(points, distanceMeters, durationSeconds, "google");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google Directions request failed — falling back to straight line.");
            return DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub");
        }
    }
}
