using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;

namespace Servika.Application.Referrals;

/// <summary>
/// Awards the referral reward when a referred artisan completes their first job.
/// Called (in-transaction) right after a booking is confirmed complete — by the
/// customer or the auto-confirm sweep. Resolves the booking's artisan → their user
/// → any Pending referral for that user, credits the referrer's referral pool
/// (a <see cref="WalletOwnerType.Referrer"/> ledger entry) and marks it Earned.
/// Idempotent: only the first completion for that referred artisan pays out.
/// </summary>
public sealed class ReferralService
{
    /// <summary>Reward per successful referral, in Naira.</summary>
    public const int RewardNaira = 500;

    private readonly IReferralRepository _referrals;
    private readonly ICatalogueRepository _catalogue;
    private readonly IWalletRepository _wallet;

    public ReferralService(
        IReferralRepository referrals, ICatalogueRepository catalogue, IWalletRepository wallet)
    {
        _referrals = referrals;
        _catalogue = catalogue;
        _wallet = wallet;
    }

    /// <summary>Credits the referrer if this completed booking is the referred
    /// artisan's first. Stages the changes only; the caller's SaveChanges commits.</summary>
    public async Task AwardIfReferredAsync(Booking booking, DateTimeOffset now, CancellationToken ct)
    {
        if (booking.ArtisanId is not { } artisanProfileId) return;

        var profile = await _catalogue.GetArtisanByIdAsync(artisanProfileId, ct);
        if (profile?.UserId is not { } artisanUserId) return; // unlinked catalogue artisan

        var referral = await _referrals.FindByReferredUserAsync(artisanUserId, ct);
        if (referral is null) return;

        if (!referral.MarkEarned(now)) return; // already earned/paid — fires once

        _wallet.Add(WalletTransaction.Create(
            WalletOwnerType.Referrer, referral.ReferrerUserId,
            WalletTransactionType.ReferralBonus, referral.RewardNaira,
            booking.Id, null, "Referral reward", now));
    }
}
