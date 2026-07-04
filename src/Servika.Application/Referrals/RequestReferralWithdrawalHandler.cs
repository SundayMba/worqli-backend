using Servika.Application.Abstractions.Persistence;
using Servika.Application.Payments;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Referrals;

/// <summary>
/// The signed-in user cashes out their referral reward pool to a bank account.
/// The referral pool (<see cref="WalletOwnerType.Referrer"/>, keyed to the user's
/// own id) is separate from any booking-payment wallet, and shares the same payout
/// rails as artisan earnings via <see cref="WithdrawalService"/>. The floor is one
/// referral reward (admin-configured), so a single earned referral is cashable.
/// </summary>
public sealed class RequestReferralWithdrawalHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly WithdrawalService _service;

    public RequestReferralWithdrawalHandler(
        IPlatformSettingsRepository settings, WithdrawalService service)
    {
        _settings = settings;
        _service = service;
    }

    public async Task<WithdrawalDto> HandleAsync(
        Guid userId, RequestWithdrawalRequest request, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        return await _service.WithdrawAsync(
            WalletOwnerType.Referrer, userId, userId,
            request, settings.ReferralRewardNaira, ct);
    }
}
