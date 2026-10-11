using Servika.Application.Abstractions.Persistence;
using Servika.Application.Catalogue;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// The artisan's own work rules applied to an open request: away mode, work radius
/// and whether they take emergencies. Shared by the open-job broadcast and the open
/// pool so both always agree on who sees what. (Being offline is handled separately:
/// offline artisans get no broadcasts but may still browse the pool.)
/// </summary>
public static class OpenJobMatching
{
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
        if (!Accepts(RulesOf(p), booking, now)) return $"This job is outside your {p.WorkRadiusKm} km work area. Widen it in Availability to take it.";
        return null;
    }

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
