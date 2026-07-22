using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// The customer picks how the agreed price gets settled: online escrow
/// (default, protected) or cash after service (the fallback — no escrow).
/// Choosing cash opens the pay-before-work gate immediately and tells the
/// artisan; switching back to online closes it again until payment lands.
/// </summary>
public sealed class ChoosePaymentMethodHandler
{
    private readonly IBookingRepository _bookings;
    private readonly Notifications.NotificationEmitter _notifications;

    public ChoosePaymentMethodHandler(
        IBookingRepository bookings, Notifications.NotificationEmitter notifications)
    {
        _bookings = bookings;
        _notifications = notifications;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, string? method, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var parsed = method?.Trim().ToLowerInvariant() switch
        {
            "cash" => PaymentMethod.Cash,
            "online" or null or "" => PaymentMethod.Online,
            _ => throw new ArgumentException("Payment method must be 'online' or 'cash'."),
        };

        var changed = booking.PaymentMethod != parsed;
        booking.ChoosePaymentMethod(parsed);

        // Only a switch TO cash matters to the artisan — it clears their gate.
        if (changed && parsed == PaymentMethod.Cash)
            await _notifications.ArtisanCashChosen(booking, ct);

        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto();
    }
}
