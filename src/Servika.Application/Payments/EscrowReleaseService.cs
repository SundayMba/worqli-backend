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
/// <para>A booking can hold more than one payment (a diagnosis fee, then the approved
/// repairs); each is released once. Mirrors <see cref="CashCommissionService"/>: stages
/// onto the shared DbContext (no <c>SaveChanges</c>), so it commits inside the caller's
/// completion transaction. A no-op for a cash/unpaid booking (nothing in escrow) and
/// idempotent (only releases Succeeded, not-yet-released payments).</para>
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

    /// <summary>Releases every held escrow payment on a completed booking. Returns the
    /// artisan earning released (0 when there was nothing in escrow).</summary>
    public async Task<int> ReleaseIfPaidAsync(Booking booking, CancellationToken ct)
    {
        var payments = await _payments.ListSucceededForBookingAsync(booking.Id, ct);
        // A materials advance the customer already released comes out of the earning once, so the
        // artisan receives the agreed total exactly once.
        var advanceLeft = booking.ReleasedMaterialsAdvanceNaira;
        var released = 0;
        foreach (var payment in payments.OrderBy(p => p.CreatedAt))
        {
            if (payment.IsEarningReleased) continue; // already released — idempotent
            var deduct = Math.Min(advanceLeft, payment.ArtisanEarningNaira);
            advanceLeft -= deduct;
            released += Release(booking, payment, deduct);
        }
        return released;
    }

    /// <summary>Releases one held payment: commission to the platform, the rest (less any materials advance
    /// already paid out) to the artisan who did the job.</summary>
    public int Release(Booking booking, Payment payment, int advanceDeductedNaira = 0)
    {
        if (payment.IsEarningReleased) return 0;
        var now = _clock.UtcNow;

        // The artisan who did the job earns it (an upfront payment was taken before anyone was assigned).
        if (booking.ArtisanId is { } doneBy && payment.ArtisanId != doneBy)
            payment.AttributeTo(doneBy);

        // Platform commission (skipped at 0% launch rate).
        if (payment.CommissionNaira > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Platform, WalletTransaction.PlatformOwnerId,
                WalletTransactionType.PlatformCommission, payment.CommissionNaira,
                booking.Id, payment.Id,
                $"Commission on booking {booking.Id}", now));

        // The artisan's earning — now, and only now, becomes available to withdraw.
        var earning = Math.Max(0, payment.ArtisanEarningNaira - advanceDeductedNaira);
        if (payment.ArtisanId is { } artisanId && earning > 0)
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Artisan, artisanId,
                WalletTransactionType.ArtisanEarning, earning,
                booking.Id, payment.Id,
                advanceDeductedNaira > 0
                    ? $"Earning for booking {booking.Id} (after ₦{advanceDeductedNaira:N0} materials advance)"
                    : $"Earning for booking {booking.Id}", now));

        payment.MarkEarningReleased(now);
        return earning;
    }
}
