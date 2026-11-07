using Servika.Application.Abstractions.Persistence;
using Servika.Application.Catalogue;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// The artisan's own work rules applied to an open request: away mode, work radius
/// and whether they take emergencies. The work radius decides who gets the instant
/// alert (<see cref="Accepts"/>); the open pool and bidding reach further, to any job
/// in the artisan's trades within <see cref="PoolMaxKm"/> (about one state, e.g. all of
/// Lagos), nearest first (<see cref="PoolAccepts"/>). Offline artisans get no alerts
/// but may still browse the pool.
/// </summary>
public static class OpenJobMatching
{
    /// <summary>How far the open pool reaches from the artisan's base. Lagos end to end is
    /// roughly 80 km, so this keeps the pool to one state without storing states.</summary>
    public const double PoolMaxKm = 80;

    public static ArtisanWorkRules RulesOf(ArtisanProfile p) =>
        new(p.Latitude, p.Longitude, p.WorkRadiusKm, p.AcceptsEmergency, p.AwayUntilUtc);

    /// <summary>True when an artisan away until a future moment.</summary>
    public static bool IsAway(DateTimeOffset? awayUntilUtc, DateTimeOffset now) =>
        awayUntilUtc is { } until && until > now;

    /// <summary>Why this artisan's own rules keep them off this job, or null when they fit.
    /// Used by claim and bid so a job opened by id obeys the same rules as the pool.</summary>
    public static string? RefusalReason(ArtisanProfile p, Booking booking, DateTimeOffset now)
    {
        if (IsAway(p.AwayUntilUtc, now)) return "You are in away mode. Turn it off to take new jobs.";
        if (!p.AcceptsEmergency && booking.Urgency == Urgency.Urgent) return "You switched off urgent jobs. Turn them on in Availability to take this one.";
        if (!PoolAccepts(RulesOf(p), booking, now)) return "This job is too far from your base to take.";
        return null;
    }

    /// <summary>Straight-line km between the job and the artisan's base, or null when either is not pinned.</summary>
    public static double? DistanceKm(ArtisanWorkRules rules, Booking booking) =>
        booking.LocationLat is { } jobLat && booking.LocationLng is { } jobLng
        && rules.Latitude is { } artLat && rules.Longitude is { } artLng
            ? GeoDistance.Km(jobLat, jobLng, artLat, artLng)
            : null;

    /// <summary>Who may see and bid on an open job: not away, takes urgent work if it is urgent,
    /// and within <see cref="PoolMaxKm"/> when both ends are pinned. The work radius does not apply.</summary>
    public static bool PoolAccepts(ArtisanWorkRules rules, Booking booking, DateTimeOffset now)
    {
        if (IsAway(rules.AwayUntilUtc, now)) return false;
        if (!rules.AcceptsEmergency && booking.Urgency == Urgency.Urgent) return false;
        return DistanceKm(rules, booking) is not { } km || km <= PoolMaxKm;
    }

    /// <summary>Who gets the instant alert for a new open job: the pool rules plus the artisan's own work radius.</summary>
    public static bool Accepts(ArtisanWorkRules rules, Booking booking, DateTimeOffset now)
    {
        if (IsAway(rules.AwayUntilUtc, now)) return false;
        if (!rules.AcceptsEmergency && booking.Urgency == Urgency.Urgent) return false;
        // Radius applies only when both ends are pinned; otherwise we can't judge it.
        if (rules.WorkRadiusKm > 0
            && booking.LocationLat is { } jobLat && booking.LocationLng is { } jobLng
            && rules.Latitude is { } artLat && rules.Longitude is { } artLng
            && GeoDistance.Km(jobLat, jobLng, artLat, artLng) > rules.WorkRadiusKm)
            return false;
        return true;
    }
}
