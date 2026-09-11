using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Starts a payment for one of the customer's own bookings. The amount is taken
/// from the booking (never the client), a Pending <see cref="Payment"/> is recorded
/// with the gateway reference, and the hosted checkout URL is returned. Calling it
/// again while a Pending payment exists returns that same one (idempotent), and a
/// booking that's already paid is a 409.
/// </summary>
public sealed class InitializePaymentHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IPaymentRepository _payments;
    private readonly IUserRepository _users;
    private readonly IPaymentGateway _gateway;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public InitializePaymentHandler(
        IBookingRepository bookings,
        IPaymentRepository payments,
        IUserRepository users,
        IPaymentGateway gateway,
        IPlatformSettingsRepository settings,
        IClock clock)
    {
        _bookings = bookings;
        _payments = payments;
        _users = users;
        _gateway = gateway;
        _settings = settings;
        _clock = clock;
    }

    public async Task<PaymentInitResponse> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        if (booking.PaymentState == Domain.Bookings.BookingPaymentState.Paid)
            throw new ConflictException("This booking has already been paid.");

        if (booking.InitialQuoteAmountNaira is not { } amount || amount <= 0)
            throw new ConflictException("This booking has nothing due to pay yet.");

        // Payment starts only once the job is actually on: a fixed-price booking
        // carries its amount from creation, but paying before the artisan accepts
        // would need a refund path if they decline — so the gate opens at Accepted.
        if (booking.Status is Domain.Bookings.BookingStatus.Open
            or Domain.Bookings.BookingStatus.Pending
            or Domain.Bookings.BookingStatus.Rejected
            or Domain.Bookings.BookingStatus.Cancelled
            or Domain.Bookings.BookingStatus.Expired)
        {
            throw new ConflictException(
                "Payment opens once the artisan accepts your booking.");
        }

        // Reuse an in-flight Pending payment so a retry doesn't create duplicates.
        var existing = await _payments.FindActiveForBookingAsync(bookingId, ct);
        if (existing is { Status: PaymentStatus.Pending })
            return ToResponse(existing);

        var user = await _users.FindByIdAsync(customerId, ct);
        var email = string.IsNullOrWhiteSpace(user?.Email)
            ? "customer@servika.app"
            : user!.Email;

        // Transaction fee: 0 while Servika covers it (launch window); once users bear
        // fees, the gateway's charge is added ON TOP so the escrow still holds the
        // full agreed price. Computed here, from settings, never from the client.
        var settings = await _settings.GetOrCreateAsync(ct);
        var now = _clock.UtcNow;
        var serviceFee = FeePolicy.UsersBearFees(settings, now)
            ? FeePolicy.CustomerServiceFee(amount, settings)
            : 0;

        var reference = $"svk_{Guid.NewGuid():N}";
        var result = await _gateway.InitializeAsync(
            new PaymentInitInput(
                reference, amount + serviceFee, email, bookingId,
                PaymentReturnLinks.CustomerBooking(bookingId)), ct);

        var payment = Payment.Initiate(
            bookingId: bookingId,
            customerId: customerId,
            artisanId: booking.ArtisanId,
            amountNaira: amount,
            commissionRate: booking.CommissionRate,
            provider: _gateway.Provider,
            reference: result.Reference,
            authorizationUrl: result.AuthorizationUrl,
            now: now,
            serviceFeeNaira: serviceFee);

        _payments.Add(payment);
        booking.MarkPaymentPending();
        await _payments.SaveChangesAsync(ct);

        return ToResponse(payment);
    }

    private static PaymentInitResponse ToResponse(Payment p) =>
        new(p.Id, p.Status.ToString(), p.Reference, p.AuthorizationUrl, p.AmountNaira, p.ServiceFeeNaira, p.ChargedNaira);
}
