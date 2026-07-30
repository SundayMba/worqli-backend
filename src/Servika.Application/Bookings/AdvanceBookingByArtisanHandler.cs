using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Advances one of the artisan's assigned jobs through the booking state machine
/// (accept / reject / on-my-way / arrived / start work). Ownership is enforced
/// twice: the caller's user id resolves to *their* artisan profile, and the
/// repository only returns a booking assigned to that profile — another artisan's
/// (or an unassigned) job is a 404, never actionable. The legality of each
/// transition (e.g. can't Arrive before OnMyWay) lives in the <c>Booking</c>
/// entity, which throws <c>InvalidBookingStateException</c> (→ 409).
/// </summary>
public sealed class AdvanceBookingByArtisanHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly NotificationEmitter _notifications;
    private readonly Payments.RefundService _refunds;
    private readonly IClock _clock;

    public AdvanceBookingByArtisanHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue,
        NotificationEmitter notifications, Payments.RefundService refunds, IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _notifications = notifications;
        _refunds = refunds;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, ArtisanBookingAction action, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var now = _clock.UtcNow;
        switch (action)
        {
            case ArtisanBookingAction.Accept: booking.Accept(now); break;
            case ArtisanBookingAction.Reject: booking.Reject(); break;
            case ArtisanBookingAction.StartTrip: booking.StartTrip(); break;
            case ArtisanBookingAction.Arrive: booking.Arrive(); break;
            case ArtisanBookingAction.StartWork: booking.StartWork(_clock.UtcNow); break;
            case ArtisanBookingAction.Cancel:
                booking.Cancel(now);
                // Any escrow the customer already paid goes straight back in full
                // (no-op if unpaid). Same transaction as the cancellation.
                await _refunds.RefundIfPaidAsync(booking, ct);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown artisan action.");
        }

        // Notify the customer of the transition; flushed with the booking below.
        _notifications.BookingAdvancedByArtisan(booking, action);

        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto();
    }
}
