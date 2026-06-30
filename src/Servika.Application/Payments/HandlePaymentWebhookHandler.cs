using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Applies a gateway payment webhook. The signature is verified first (a forged
/// body is a 401), then the event is matched to our Pending payment by reference.
/// It is <b>idempotent</b>: an unknown reference or an already-settled payment is a
/// no-op, so the gateway can safely retry. On success it advances the payment, marks
/// the booking paid, and writes the append-only ledger split
/// (customer debit, platform commission, artisan earning) in one transaction.
/// </summary>
public sealed class HandlePaymentWebhookHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IWalletRepository _wallet;
    private readonly IBookingRepository _bookings;
    private readonly IPaymentGateway _gateway;
    private readonly IClock _clock;

    public HandlePaymentWebhookHandler(
        IPaymentRepository payments,
        IWalletRepository wallet,
        IBookingRepository bookings,
        IPaymentGateway gateway,
        IClock clock)
    {
        _payments = payments;
        _wallet = wallet;
        _bookings = bookings;
        _gateway = gateway;
        _clock = clock;
    }

    public async Task HandleAsync(string rawBody, string? signature, CancellationToken ct)
    {
        if (!_gateway.VerifySignature(rawBody, signature))
            throw new InvalidWebhookSignatureException();

        var evt = _gateway.ParseWebhook(rawBody);
        if (evt is null)
            return; // not an event we care about

        var payment = await _payments.FindByReferenceAsync(evt.Reference, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return; // unknown or already settled — idempotent no-op

        var now = _clock.UtcNow;

        if (evt.Outcome == PaymentWebhookOutcome.Failed)
        {
            payment.MarkFailed(now);
            await _payments.SaveChangesAsync(ct);
            return;
        }

        // Succeeded → settle + record the ledger split.
        payment.MarkSucceeded(now);

        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Customer, payment.CustomerId,
            WalletTransactionType.BookingPayment, -payment.AmountNaira,
            payment.BookingId, payment.Id,
            $"Payment for booking {payment.BookingId}", now));

        if (payment.CommissionNaira > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.PlatformCommission, payment.CommissionNaira,
                payment.BookingId, payment.Id,
                $"Commission on booking {payment.BookingId}", now));

        if (payment.ArtisanId is { } artisanId && payment.ArtisanEarningNaira > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Artisan, artisanId,
                WalletTransactionType.ArtisanEarning, payment.ArtisanEarningNaira,
                payment.BookingId, payment.Id,
                $"Earning for booking {payment.BookingId}", now));

        var booking = await _bookings.FindByIdAsync(payment.BookingId, ct);
        booking?.MarkPaid();

        await _payments.SaveChangesAsync(ct);
    }
}
