using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Notifications;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Applies a provider's verdict on a Pending payment: the one place a payment is
/// settled, whether the verdict arrived on the webhook or was fetched by the verify
/// endpoint when the payer came back to the app first. Stages changes only; the
/// caller saves. Guards: never settles for less than we charged or in a currency
/// other than NGN (payment failed, admins alerted); a non-Pending payment is a no-op.
/// </summary>
public sealed class PaymentSettlementService
{
    private readonly IWalletRepository _wallet;
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public PaymentSettlementService(
        IWalletRepository wallet,
        IBookingRepository bookings,
        NotificationEmitter notifications,
        IPlatformSettingsRepository settings,
        IClock clock)
    {
        _wallet = wallet;
        _bookings = bookings;
        _notifications = notifications;
        _settings = settings;
        _clock = clock;
    }

    /// <summary>Returns true when the payment left Pending (settled or failed).</summary>
    public async Task<bool> ApplyAsync(Payment payment, PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Pending) return false;
        if (evt.Outcome == PaymentWebhookOutcome.Pending) return false;

        var now = _clock.UtcNow;

        if (evt.Outcome == PaymentWebhookOutcome.Failed)
        {
            payment.MarkFailed(now);
            return true;
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
            return true;
        }

        payment.MarkSucceeded(now);

        // What the gateway kept: reported by the provider (kobo), else our own estimate.
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

        // A commission settlement has no booking and no split: it credits the
        // artisan's ledger, which clears the debt (and any standing restriction).
        if (payment.Purpose == PaymentPurpose.CommissionSettlement)
        {
            if (payment.ArtisanId is { } settledArtisanId)
                _wallet.Add(WalletTransaction.Create(
                    WalletOwnerType.Artisan, settledArtisanId,
                    WalletTransactionType.CommissionSettlement, payment.AmountNaira,
                    null, payment.Id, "Service-fee settlement", now));
            _notifications.ArtisanBalanceSettled(payment.CustomerId, payment.AmountNaira);
            return true;
        }

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

        // Escrow: only the customer's debit is written now. The money is HELD; the
        // artisan's earning + platform commission are released at completion
        // (EscrowReleaseService), never at payment.
        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Customer, payment.CustomerId,
            WalletTransactionType.BookingPayment, -payment.AmountNaira,
            payment.BookingId, payment.Id,
            $"Payment for booking {payment.BookingId}", now));

        var booking = await _bookings.FindByIdAsync(payment.BookingId!.Value, ct);
        booking?.MarkPaid();

        _notifications.PaymentReceived(
            payment.CustomerId, payment.BookingId!.Value, booking?.ServiceName ?? string.Empty);
        if (booking is not null)
            await _notifications.ArtisanEscrowFunded(booking, ct);
        return true;
    }
}
