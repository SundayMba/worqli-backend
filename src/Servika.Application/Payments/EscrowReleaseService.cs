using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Releases a booking's escrow at COMPLETION. This is what makes the escrow real:
/// when the customer pays, only the customer's debit is written and the money is
/// HELD. The artisan's earning and the platform's commission are written here, once
/// the job is actually confirmed complete — so an artisan can never withdraw money
/// for work that isn't finished, and a refund before completion has nothing to claw
/// back.
///
/// <para>Mirrors <see cref="CashCommissionService"/>: stages onto the shared
/// DbContext (no <c>SaveChanges</c>), so it commits inside the caller's completion
/// transaction. A no-op for a cash/unpaid booking (nothing in escrow) and idempotent
/// (only releases a Succeeded, not-yet-released payment, once).</para>
/// </summary>
public sealed class EscrowReleaseService
{
    private readonly IPaymentRepository _payments;
    private readonly IWalletRepository _wallet;
    private readonly IClock _clock;

    public EscrowReleaseService(
        IPaymentRepository payments,
        IWalletRepository wallet,
        IClock clock)
    {
        _payments = payments;
        _wallet = wallet;
        _clock = clock;
    }

    /// <summary>Releases the held escrow split for a completed booking, if it was
    /// paid online and not already released. Returns the artisan earning released
    /// (0 when there was nothing in escrow).</summary>
    public async Task<int> ReleaseIfPaidAsync(Booking booking, CancellationToken ct)
    {
        var payment = await _payments.FindSucceededForBookingAsync(booking.Id, ct);
        if (payment is null) return 0;            // cash or never paid — nothing held
        if (payment.IsEarningReleased) return 0;  // already released — idempotent

        var now = _clock.UtcNow;

        // Platform commission (skipped at 0% launch rate).
        if (payment.CommissionNaira > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.PlatformCommission, payment.CommissionNaira,
                booking.Id, payment.Id,
                $"Commission on booking {booking.Id}", now));

        // The artisan's earning — now, and only now, becomes available to withdraw.
        // Any materials advance the customer already released comes out of it, so
        // the artisan receives the agreed total exactly once.
        var earning = Math.Max(0, payment.ArtisanEarningNaira - booking.ReleasedMaterialsAdvanceNaira);
        if (payment.ArtisanId is { } artisanId && earning > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Artisan, artisanId,
                WalletTransactionType.ArtisanEarning, earning,
                booking.Id, payment.Id,
                booking.ReleasedMaterialsAdvanceNaira > 0
                    ? $"Earning for booking {booking.Id} (after ₦{booking.ReleasedMaterialsAdvanceNaira:N0} materials advance)"
                    : $"Earning for booking {booking.Id}", now));

        payment.MarkEarningReleased(now);
        return earning;
    }
}
