using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
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
    private readonly NotificationEmitter _notifications;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public HandlePaymentWebhookHandler(
        IPaymentRepository payments,
        IWalletRepository wallet,
        IBookingRepository bookings,
        IPaymentGateway gateway,
        NotificationEmitter notifications,
        IPlatformSettingsRepository settings,
        IClock clock)
    {
        _payments = payments;
        _wallet = wallet;
        _bookings = bookings;
        _gateway = gateway;
        _notifications = notifications;
        _settings = settings;
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

        // Security: never settle for less than we asked, or in another currency. A
        // forged or tampered "success" for ₦1 must not mark a ₦50,000 booking paid.
        // The payment is failed (a fresh init can be started) and every admin is told.
        if (evt.Currency is { } currency && !string.Equals(currency, "NGN", StringComparison.OrdinalIgnoreCase)
            || evt.AmountKobo is { } paidKobo && paidKobo < (long)payment.ChargedNaira * 100)
        {
            payment.MarkFailed(now);
            await _notifications.PaymentAmountMismatchAsync(
                payment.BookingId, payment.Reference, payment.ChargedNaira,
                evt.AmountKobo is { } k ? (int)(k / 100) : null, evt.Currency, ct);
            await _payments.SaveChangesAsync(ct);
            return;
        }

        // Succeeded → settle + record the ledger split.
        payment.MarkSucceeded(now);

        // What the gateway kept: reported on the webhook (kobo), else our own estimate.
        var settings = await _settings.GetOrCreateAsync(ct);
        var gatewayFee = evt.FeesKobo is { } feesKobo
            ? (int)Math.Round(feesKobo / 100m, MidpointRounding.AwayFromZero)
            : FeePolicy.CardFee(payment.ChargedNaira, settings);
        payment.RecordGatewayFee(gatewayFee);
        if (gatewayFee > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.GatewayCost, -gatewayFee,
                payment.BookingId, payment.Id,
                $"Gateway charge on payment {payment.Reference}", now));

        // The fee the customer paid on top (only once users bear fees): theirs out,
        // Servika's in. It is never escrow and never the artisan's.
        if (payment.ServiceFeeNaira > 0)
        {
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Customer, payment.CustomerId,
                WalletTransactionType.ServiceFee, -payment.ServiceFeeNaira,
                payment.BookingId, payment.Id, "Payment fee", now));
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.ServiceFee, payment.ServiceFeeNaira,
                payment.BookingId, payment.Id,
                $"Payment fee collected on {payment.Reference}", now));
        }

        // A commission settlement has no booking and no split — it simply
        // credits the artisan's ledger, which clears the debt (and any standing
        // restriction) the moment it lands.
        if (payment.Purpose == PaymentPurpose.CommissionSettlement)
        {
            if (payment.ArtisanId is { } settledArtisanId)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Artisan, settledArtisanId,
                    WalletTransactionType.CommissionSettlement, payment.AmountNaira,
                    null, payment.Id,
                    "Service-fee settlement", now));
            // The payer IS the artisan's login account for settlements.
            _notifications.ArtisanBalanceSettled(payment.CustomerId, payment.AmountNaira);
            await _payments.SaveChangesAsync(ct);
            return;
        }

        // Escrow: only the customer's debit is written now. The money is HELD —
        // the artisan's earning + platform commission are released to the ledger
        // at completion (EscrowReleaseService), never at payment, so an artisan
        // cannot withdraw for work that isn't done.
        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Customer, payment.CustomerId,
            WalletTransactionType.BookingPayment, -payment.AmountNaira,
            payment.BookingId, payment.Id,
            $"Payment for booking {payment.BookingId}", now));

        var booking = await _bookings.FindByIdAsync(payment.BookingId!.Value, ct);
        booking?.MarkPaid();

        _notifications.PaymentReceived(
            payment.CustomerId, payment.BookingId!.Value, booking?.ServiceName ?? string.Empty);
        // The pay-before-work gate keys off this — tell the artisan they're clear.
        if (booking is not null)
            await _notifications.ArtisanEscrowFunded(booking, ct);

        await _payments.SaveChangesAsync(ct);
    }
}
