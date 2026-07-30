namespace Servika.Contracts.Tracking;

/// <summary>
/// A live-location ping sent over REST (POST /api/v1/tracking/bookings/{id}/ping)
/// — the artisan's fallback when the hub WebSocket is down. Mirrors the hub's
/// SendLocationUpdate arguments.
/// </summary>
public sealed record TrackingPingRequest(
    double Latitude,
    double Longitude,
    double? Accuracy = null,
    double? Heading = null,
    double? Speed = null);
