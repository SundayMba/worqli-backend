using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Disputes;
using Servika.Domain.Disputes;

namespace Servika.Application.Disputes;

/// <summary>
/// The customer raises a dispute about their booking. Guards (all scoped to the
/// caller): the booking must be theirs (404 if not); it must be in a disputable
/// state — InProgress / AwaitingConfirmation / Completed (409, a Domain invariant);
/// and it must not already have an open dispute (409). The dispute row and the
/// booking's move to Disputed commit together in one transaction.
/// </summary>
public sealed class RaiseDisputeHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IDisputeRepository _disputes;
    private readonly IUserRepository _users;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;
    private readonly Abstractions.Notifications.IAdminAlerts _alerts;

    public RaiseDisputeHandler(
        IBookingRepository bookings,
        IDisputeRepository disputes,
        IUserRepository users,
        Notifications.NotificationEmitter notifications,
        IClock clock,
        Abstractions.Notifications.IAdminAlerts alerts)
    {
        _bookings = bookings;
        _disputes = disputes;
        _users = users;
        _notifications = notifications;
        _clock = clock;
        _alerts = alerts;
    }

    public async Task<DisputeDto> HandleAsync(
        Guid customerId, Guid bookingId, RaiseDisputeRequest request, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        if (await _disputes.HasOpenForBookingAsync(bookingId, ct))
            throw new ConflictException("There is already an open dispute for this booking.");

        var customer = await _users.FindByIdAsync(customerId, ct);
        var now = _clock.UtcNow;

        var dispute = Dispute.Raise(
            bookingId: booking.Id,
            raisedByUserId: customerId,
            customerName: customer?.FullName ?? "Customer",
            serviceName: booking.ServiceName,
            category: request.Category,
            description: request.Description,
            now: now);

        _disputes.Add(dispute);
        booking.RaiseDispute(now); // InProgress/AwaitingConfirmation/Completed → Disputed
        await _notifications.DisputeRaisedForArtisanAsync(booking, ct); // let the artisan respond
        await _disputes.SaveChangesAsync(ct); // commits the dispute + booking + notification together

        _alerts.Send(new Abstractions.Notifications.AdminAlert(Abstractions.Notifications.AdminAlertKind.DisputeRaised,
            "Customer raised a dispute",
            new[] { $"{dispute.CustomerName} · {booking.ServiceName}{(string.IsNullOrWhiteSpace(booking.ArtisanName) ? "" : $" with {booking.ArtisanName}")}",
                    $"{request.Category}. The booking is frozen until you resolve it." },
            "/disputes", "Open disputes"));

        return dispute.ToDto();
    }
}
