using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Referrals;
using Servika.Domain.Catalogue;
using Servika.Domain.Payments;
using Servika.Domain.Referrals;

namespace Servika.Application.Referrals;

/// <summary>
/// The signed-in user's referral dashboard: their share code (assigned lazily),
/// referral earnings (from the <see cref="WalletOwnerType.Referrer"/> pool — kept
/// separate from any booking-payment wallet), and the artisans they referred with
/// a derived status.
/// </summary>
public sealed class GetMyReferralsHandler
{
    private readonly IUserRepository _users;
    private readonly IReferralRepository _referrals;
    private readonly ICatalogueRepository _catalogue;
    private readonly IWalletRepository _wallet;
    private readonly IPlatformSettingsRepository _settings;

    public GetMyReferralsHandler(
        IUserRepository users,
        IReferralRepository referrals,
        ICatalogueRepository catalogue,
        IWalletRepository wallet,
        IPlatformSettingsRepository settings)
    {
        _users = users;
        _referrals = referrals;
        _catalogue = catalogue;
        _wallet = wallet;
        _settings = settings;
    }

    public async Task<ReferralSummaryDto> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        // Assign a share code on first view.
        if (string.IsNullOrWhiteSpace(user.ReferralCode))
        {
            string code;
            do { code = ReferralCodeGenerator.Generate(user.FullName); }
            while (await _users.FindByReferralCodeAsync(code, ct) is not null);
            user.SetReferralCode(code);
            await _users.SaveChangesAsync(ct);
        }

        var entries = await _wallet.ListForOwnerAsync(WalletOwnerType.Referrer, userId, ct);
        var available = entries.Sum(e => e.AmountNaira);
        var totalEarned = entries
            .Where(e => e.Type == WalletTransactionType.ReferralBonus)
            .Sum(e => e.AmountNaira);
        var paidOut = totalEarned - available;

        var referrals = await _referrals.ListForReferrerAsync(userId, ct);
        var referred = new List<ReferredArtisanDto>();
        var pending = 0;
        foreach (var r in referrals)
        {
            if (r.Status == ReferralStatus.Pending) pending++;
            var refUser = await _users.FindByIdAsync(r.ReferredUserId, ct);
            var profile = await _catalogue.GetArtisanByUserIdAsync(r.ReferredUserId, ct);
            referred.Add(new ReferredArtisanDto(
                r.Id,
                refUser?.FullName ?? "Invited artisan",
                profile?.Specialty ?? "Artisan",
                DeriveStatus(r.Status, profile),
                r.CreatedAt));
        }

        var rewardNaira = (await _settings.GetOrCreateAsync(ct)).ReferralRewardNaira;
        return new ReferralSummaryDto(
            user.ReferralCode!, rewardNaira, available, paidOut, pending, referred);
    }

    private static string DeriveStatus(ReferralStatus status, ArtisanProfile? profile) => status switch
    {
        ReferralStatus.Paid => "paid",
        ReferralStatus.Earned => "earned",
        _ => profile?.VerificationStatus == ArtisanVerificationStatus.Verified ? "active" : "onboarding",
    };
}
