namespace Servika.Contracts.Tracking;

/// <summary>
/// A single live-location ping broadcast to everyone watching a booking's trip
/// (the customer). Sent as the payload of the SignalR <c>LocationUpdated</c>
/// event. Optional fields (<paramref name="Accuracy"/>, <paramref name="Heading"/>,
/// <paramref name="Speed"/>) are whatever the device could supply.
/// </summary>
public sealed record LocationUpdate(
    Guid BookingId,
    double Latitude,
    double Longitude,
    double? Accuracy,
    double? Heading,
    double? Speed,
    DateTimeOffset AtUtc);
