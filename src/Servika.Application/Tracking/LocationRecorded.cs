using Servika.Contracts.Tracking;

namespace Servika.Application.Tracking;

/// <summary>
/// Outcome of recording an artisan location ping. <paramref name="Started"/> is
/// true when this ping opened a fresh session (so the hub can announce
/// <c>TrackingStarted</c> before the first <c>LocationUpdated</c>).
/// </summary>
public sealed record LocationRecorded(bool Started, LocationUpdate Update);
