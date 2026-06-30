namespace Servika.Application.Common;

/// <summary>
/// Thrown when a live-tracking action isn't permitted — the caller doesn't own
/// the booking, the booking isn't in a trackable state (OnMyWay), or the update
/// is otherwise rejected. Surfaced over SignalR as a <c>TrackingError</c> event
/// (there's no HTTP status here — the hub catches it and notifies the caller).
/// </summary>
public sealed class TrackingNotAllowedException : Exception
{
    public TrackingNotAllowedException(string message)
        : base(message)
    {
    }
}
