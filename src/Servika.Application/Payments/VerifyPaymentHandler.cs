using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// The payer asks how their payment stands. If it is still Pending we ask the
/// provider directly and, when it has a final answer, settle through the same
/// <see cref="PaymentSettlementService"/> the webhook uses, so a late webhook is
/// then a harmless no-op. Only the payer may ask (404 otherwise), and the provider
/// call is the only thing that can move the state: the client sends no verdict.
/// </summary>
public sealed class VerifyPaymentHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IBookingRepository _bookings;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentSettlementService _settlement;

    public VerifyPaymentHandler(
        IPaymentRepository payments,
        IBookingRepository bookings,
        IPaymentGateway gateway,
        PaymentSettlementService settlement)
    {
        _payments = payments;
        _bookings = bookings;
        _gateway = gateway;
        _settlement = settlement;
    }

    public async Task<PaymentStatusDto> HandleAsync(Guid userId, string reference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
            throw new ArgumentException("A payment reference is required.");

        var payment = await _payments.FindByReferenceAsync(reference, ct);
        if (payment is null || payment.CustomerId != userId)
            throw new NotFoundException("No such payment.");

        var settledNow = false;
        if (payment.Status == PaymentStatus.Pending)
        {
            var verdict = await _gateway.VerifyAsync(payment.Reference, ct);
            if (verdict is not null && await _settlement.ApplyAsync(payment, verdict, ct))
            {
                await _payments.SaveChangesAsync(ct);
                settledNow = payment.Status == PaymentStatus.Succeeded;
            }
        }

        string? bookingState = null;
        if (payment.BookingId is { } bookingId)
            bookingState = (await _bookings.FindByIdAsync(bookingId, ct))?.PaymentState.ToString();

        return new PaymentStatusDto(
            payment.Id, payment.Reference, payment.Status.ToString(),
            payment.AmountNaira, payment.ServiceFeeNaira, payment.ChargedNaira,
            payment.BookingId, bookingState, settledNow);
    }
}
