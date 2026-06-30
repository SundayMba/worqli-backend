namespace Servika.Domain.Tracking;

/// <summary>
/// A live-tracking session for one booking: the artisan's trip to the customer.
/// Created when the artisan starts pushing their location (while the booking is
/// <c>OnMyWay</c>) and ended on arrival, cancellation, or staleness. Holds the
/// latest known position so a customer joining late sees where the artisan is
/// without waiting for the next ping. Like every Domain entity it knows nothing
/// about SignalR or the database — only the session's data and its rules.
/// </summary>
public sealed class TrackingSession
{
    public Guid Id { get; private set; }

    /// <summary>The booking this trip belongs to (one active session per booking).</summary>
    public Guid BookingId { get; private set; }

    /// <summary>The artisan profile making the trip (the only one allowed to update).</summary>
    public Guid ArtisanProfileId { get; private set; }

    /// <summary>The customer who may watch this session.</summary>
    public Guid CustomerId { get; private set; }

    public TrackingStatus Status { get; private set; }

    public double? LastLatitude { get; private set; }
    public double? LastLongitude { get; private set; }
    public double? LastAccuracy { get; private set; }
    public double? LastHeading { get; private set; }
    public double? LastSpeed { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    /// <summary>When the latest location ping landed — drives stale cleanup.</summary>
    public DateTimeOffset? LastUpdateAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    private TrackingSession() { }

    /// <summary>Opens a new active session for an artisan's trip to a customer.</summary>
    public static TrackingSession Start(
        Guid bookingId, Guid artisanProfileId, Guid customerId, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (artisanProfileId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanProfileId));

        return new TrackingSession
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ArtisanProfileId = artisanProfileId,
            CustomerId = customerId,
            Status = TrackingStatus.Active,
            StartedAtUtc = now,
        };
    }

    /// <summary>Records the artisan's latest position. Coordinates are validated
    /// (a bad fix is rejected rather than stored). No-op semantics for an ended
    /// session are enforced by the caller (the repo only loads active sessions).</summary>
    public void UpdateLocation(
        double latitude, double longitude,
        double? accuracy, double? heading, double? speed,
        DateTimeOffset now)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");

        LastLatitude = latitude;
        LastLongitude = longitude;
        LastAccuracy = accuracy;
        LastHeading = heading;
        LastSpeed = speed;
        LastUpdateAtUtc = now;
    }

    /// <summary>Ends the session (arrival, cancellation, or stale cleanup). Idempotent.</summary>
    public void End(DateTimeOffset now)
    {
        if (Status == TrackingStatus.Ended) return;
        Status = TrackingStatus.Ended;
        EndedAtUtc = now;
    }
}
