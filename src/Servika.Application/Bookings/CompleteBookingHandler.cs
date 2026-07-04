using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Application.Referrals;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// The customer confirms their job is complete (InProgress → Completed). Only the
/// customer (or an admin) closes a booking — the artisan can't mark their own work
/// done — so this is scoped to the booking's owner, exactly like cancel. The
/// InProgress precondition is a Domain invariant and throws (→ 409) if not met.
/// </summary>
public sealed class CompleteBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly ReferralService _referrals;
    private readonly IClock _clock;

    public CompleteBookingHandler(
        IBookingRepository bookings,
        NotificationEmitter notifications,
        ReferralService referrals,
        IClock clock)
    {
        _bookings = bookings;
        _notifications = notifications;
        _referrals = referrals;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var now = _clock.UtcNow;
        booking.ConfirmCompletion(now);
        _notifications.BookingCompleted(booking);
        await _notifications.ArtisanJobConfirmed(booking, ct);
        // First completed job for a referred artisan → credit the referrer.
        await _referrals.AwardIfReferredAsync(booking, now, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }
}
