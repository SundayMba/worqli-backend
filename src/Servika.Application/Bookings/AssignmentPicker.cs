using Servika.Application.Abstractions.Persistence;
using Servika.Application.Catalogue;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Who gets offered a job, and in what order (Business Model v1.4, decided 2026-11-02): every
/// eligible artisan whose own rules accept the job, least recently assigned first so work rotates
/// fairly and newcomers are not starved, nearest first among equals. Pure, so the rule is testable.
/// </summary>
public static class AssignmentPicker
{
    public static IReadOnlyList<ArtisanRecipient> Order(
        IEnumerable<ArtisanRecipient> candidates, Booking booking, ISet<Guid> excludedProfileIds, DateTimeOffset now) =>
        candidates
            .Where(r => !excludedProfileIds.Contains(r.ProfileId) && OpenJobMatching.Accepts(r.Rules, booking, now))
            .OrderBy(r => r.LastAssignedAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(r => DistanceKm(r, booking) ?? double.MaxValue)
            .ThenBy(r => r.ProfileId)
            .ToList();

    public static double? DistanceKm(ArtisanRecipient r, Booking booking) =>
        booking.LocationLat is { } jobLat && booking.LocationLng is { } jobLng
        && r.Rules.Latitude is { } lat && r.Rules.Longitude is { } lng
            ? GeoDistance.Km(jobLat, jobLng, lat, lng)
            : null;
}
