using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// A verified artisan claims an open request — first-come-first-served. Validates the
/// artisan (verified profile) and the job (exists, still Open, in the artisan's
/// categories), then claims it <b>atomically</b> so exactly one of many racing
/// artisans can win; the losers get a 409. On success the job becomes Accepted and
/// assigned to the claimer, and the customer is notified an artisan was found.
/// </summary>
public sealed class ClaimOpenJobHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public ClaimOpenJobHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        Notifications.NotificationEmitter notifications,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new ConflictException("Set up your artisan profile before claiming jobs.");
        if (profile.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new ConflictException("Your artisan profile must be verified before you can claim jobs.");

        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
            ?? throw new NotFoundException("Job not found.");
        if (booking.Status != BookingStatus.Open)
            throw new ConflictException("This job has already been taken.");
        if (!profile.CategorySlugs.Contains(booking.CategorySlug))
            throw new ConflictException("This job isn't in your service categories.");

        // Atomic winner-determination: only one artisan flips it Open → Accepted.
        var won = await _bookings.TryClaimAsync(bookingId, profile.Id, profile.FullName, _clock.UtcNow, ct);
        if (!won)
            throw new ConflictException("This job has already been taken.");

        // Read the committed state fresh (read-only, so no stale tracked instance),
        // notify the customer, and return the now-assigned booking.
        var claimed = await _bookings.FindByIdReadOnlyAsync(bookingId, ct)
            ?? throw new NotFoundException("Job not found.");
        _notifications.OpenJobClaimed(claimed);
        await _bookings.SaveChangesAsync(ct);

        return claimed.ToDetailDto();
    }
}
