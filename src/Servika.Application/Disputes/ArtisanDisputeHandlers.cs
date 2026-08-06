using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Disputes;

namespace Servika.Application.Disputes;

/// <summary>
/// The assigned artisan reads the dispute on one of their jobs. Scoped to their
/// profile: a booking that isn't assigned to them is a 404 (never a leak), and 404
/// too if no dispute has been raised. Mirrors the customer's
/// <see cref="GetBookingDisputeHandler"/> from the artisan side.
/// </summary>
public sealed class GetArtisanDisputeHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IBookingRepository _bookings;
    private readonly IDisputeRepository _disputes;

    public GetArtisanDisputeHandler(
        ICatalogueRepository catalogue, IBookingRepository bookings, IDisputeRepository disputes)
    {
        _catalogue = catalogue;
        _bookings = bookings;
        _disputes = disputes;
    }

    public async Task<DisputeDto> HandleAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        // Must be assigned to this artisan (404 otherwise — same guard as the job read).
        _ = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var dispute = await _disputes.FindByBookingIdAsync(bookingId, ct)
            ?? throw new NotFoundException("No dispute has been raised for this booking.");

        return dispute.ToDto();
    }
}

/// <summary>
/// The assigned artisan adds their side of a dispute. Scoped to their profile (404 if
/// the job isn't theirs); allowed while the dispute is Open or UnderReview and blocked
/// once resolved (a Domain invariant → 409). The customer and admins are notified.
/// </summary>
public sealed class RespondToDisputeHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IBookingRepository _bookings;
    private readonly IDisputeRepository _disputes;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public RespondToDisputeHandler(
        ICatalogueRepository catalogue,
        IBookingRepository bookings,
        IDisputeRepository disputes,
        NotificationEmitter notifications,
        IClock clock)
    {
        _catalogue = catalogue;
        _bookings = bookings;
        _disputes = disputes;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<DisputeDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, RespondToDisputeRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var dispute = await _disputes.FindByBookingIdForUpdateAsync(bookingId, ct)
            ?? throw new NotFoundException("No dispute has been raised for this booking.");

        dispute.RespondAsArtisan(request.Response, _clock.UtcNow); // 409 if already resolved
        await _notifications.ArtisanRespondedToDisputeAsync(booking, ct);
        await _disputes.SaveChangesAsync(ct);

        return dispute.ToDto();
    }
}
