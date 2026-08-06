using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Reconciles an asynchronous refund settlement. A refund is REQUESTED synchronously
/// when a dispute is resolved for the customer (ledger reversed, Paystack asked), but
/// the money movement settles later and Paystack confirms it with a
/// <c>refund.processed</c> or <c>refund.failed</c> webhook.
///
/// <list type="bullet">
/// <item><b>processed</b> → stamp <c>RefundSettledAtUtc</c> and tell the customer it
/// landed.</item>
/// <item><b>failed</b> → stamp <c>RefundFailedAtUtc</c> and alert admins to reprocess
/// it. The ledger refund is NOT reversed — the customer won the dispute and is still
/// owed; only the transfer failed.</item>
/// </list>
///
/// Signature-verified and idempotent: an unknown reference, a non-refunded payment, or
/// a repeated webhook is a harmless no-op.
/// </summary>
public sealed class HandleRefundWebhookHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IBookingRepository _bookings;
    private readonly IPaymentGateway _gateway;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public HandleRefundWebhookHandler(
        IPaymentRepository payments,
        IBookingRepository bookings,
        IPaymentGateway gateway,
        NotificationEmitter notifications,
        IClock clock)
    {
        _payments = payments;
        _bookings = bookings;
        _gateway = gateway;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(string rawBody, string? signature, CancellationToken ct)
    {
        if (!_gateway.VerifySignature(rawBody, signature))
            throw new InvalidWebhookSignatureException();

        var evt = _gateway.ParseRefundWebhook(rawBody);
        if (evt is null)
            return; // not a terminal refund event we act on

        var payment = await _payments.FindByReferenceAsync(evt.TransactionReference, ct);
        if (payment is null || payment.Status != PaymentStatus.Refunded)
            return; // unknown, or we never requested this refund — idempotent no-op

        var now = _clock.UtcNow;

        if (evt.Outcome == RefundWebhookOutcome.Processed)
        {
            if (payment.RefundSettledAtUtc is not null)
                return; // already confirmed — no duplicate notification
            payment.ConfirmRefundSettled(now);
            if (payment.BookingId is { } bookingId)
            {
                var booking = await _bookings.FindByIdAsync(bookingId, ct);
                _notifications.RefundSettled(
                    payment.CustomerId, bookingId, booking?.ServiceName ?? string.Empty, payment.AmountNaira);
            }
        }
        else // Failed
        {
            if (payment.RefundFailedAtUtc is not null || payment.RefundSettledAtUtc is not null)
                return; // already handled / already landed
            payment.MarkRefundFailed(now);
            if (payment.BookingId is { } bookingId)
                await _notifications.RefundFailedNeedsRetryAsync(bookingId, payment.AmountNaira, ct);
        }

        await _payments.SaveChangesAsync(ct);
    }
}
