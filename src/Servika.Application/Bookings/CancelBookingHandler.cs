using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Cancels one of the customer's own bookings. Ownership is enforced (a foreign
/// booking is a 404); the state-machine rule about when cancelling is allowed
/// lives in the <c>Booking</c> entity, which throws if the transition is illegal.
/// If escrow was already paid, it is refunded in full (ledger reversal), in the
/// same transaction as the cancellation.
/// </summary>
public sealed class CancelBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly Payments.RefundService _refunds;
    private readonly IClock _clock;
    private readonly AssignmentService _assignment;
    private readonly Payments.EscrowReleaseService _escrow;

    public CancelBookingHandler(
        IBookingRepository bookings,
        Notifications.NotificationEmitter notifications,
        Payments.RefundService refunds,
        IClock clock,
        AssignmentService assignment,
        Payments.EscrowReleaseService escrow)
    {
        _escrow = escrow;
        _bookings = bookings;
        _notifications = notifications;
        _refunds = refunds;
        _clock = clock;
        _assignment = assignment;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var now = _clock.UtcNow;
        // A paid visit (diagnosis or custom job) whose artisan is already at the door has been earned: the fee stays
        // with them and everything else paid comes back.
        var visitEarned = booking.IsDiagnosis && booking.Status == Domain.Bookings.BookingStatus.Arrived;
        booking.Cancel(now);
        // Nothing stays offered to an artisan for a job that no longer exists.
        await _assignment.WithdrawAllAsync(booking.Id, now, ct);
        // Money already held in escrow goes straight back (full refund; no-op if
        // unpaid). Stages onto the same DbContext → one transaction with the cancel.
        if (visitEarned)
        {
            var back = booking.PaidNaira - booking.DiagnosisFeeNaira!.Value;
            if (back > 0) await _refunds.PartialRefundIfPaidAsync(booking, back, ct);
            else await _escrow.ReleaseIfPaidAsync(booking, ct);
        }
        else
        {
            await _refunds.RefundIfPaidAsync(booking, ct);
        }
        // Let the assigned artisan know it's off (no-op for an open/unassigned booking).
        await _notifications.ArtisanBookingCancelled(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto(0, null, null);
    }
}
