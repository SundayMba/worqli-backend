using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Directions;

namespace Servika.Infrastructure.Directions;

/// <summary>
/// <see cref="IDirectionsProvider"/> over any OSRM-compatible routing API (OpenStreetMap
/// data, no traffic). Asks for the full route geometry as an encoded polyline (the same
/// encoding Google uses, so the decoder is shared) and returns distance + free-flow
/// duration. Any failure falls back to a straight line so the map always draws.
/// </summary>
public sealed class OsrmDirectionsProvider : IDirectionsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OsrmDirectionsOptions _options;
    private readonly ILogger<OsrmDirectionsProvider> _logger;

    public OsrmDirectionsProvider(IHttpClientFactory httpClientFactory, OsrmDirectionsOptions options, ILogger<OsrmDirectionsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<RouteResult> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        try
        {
            var inv = CultureInfo.InvariantCulture;
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            // Self-hosted OSRM and the demo server want /route/v1/driving; LocationIQ's base already ends in /directions.
            var path = baseUrl.EndsWith("/directions", StringComparison.OrdinalIgnoreCase) ? "/driving" : "/route/v1/driving";
            var url = $"{baseUrl}{path}/{fromLng.ToString(inv)},{fromLat.ToString(inv)};{toLng.ToString(inv)},{toLat.ToString(inv)}" +
                      "?overview=full&geometries=polyline&steps=false&alternatives=false";
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                url += $"&{_options.ApiKeyQueryParam}={Uri.EscapeDataString(_options.ApiKey)}";

            var client = _httpClientFactory.CreateClient("osrm");
            using var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM routing returned {Status}; falling back to straight line.", resp.StatusCode);
                return DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub");
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (code != "Ok" || !root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
            {
                _logger.LogWarning("OSRM routing code '{Code}'; falling back to straight line.", code);
                return DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub");
            }
            var route = routes[0];
            var points = DirectionsMath.DecodePolyline(route.GetProperty("geometry").GetString() ?? "");
            if (points.Count == 0)
                points = [new RoutePoint(fromLat, fromLng), new RoutePoint(toLat, toLng)];
            return new RouteResult(points, route.GetProperty("distance").GetDouble(), route.GetProperty("duration").GetDouble(), "osrm");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OSRM routing request failed; falling back to straight line.");
            return DirectionsMath.StraightLine(fromLat, fromLng, toLat, toLng, "stub");
        }
    }
}
