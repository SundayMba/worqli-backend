using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Decides whether an artisan is in good standing to receive new job requests.
/// An artisan whose ledger balance is deeper in the red than the admin-set
/// <c>MaxCommissionDebtNaira</c> (unpaid cash-job commission that hasn't been
/// auto-netted by online earnings) is <b>restricted</b>: no open-job broadcasts,
/// no claiming, no bidding — until they settle. Settling reactivates instantly,
/// because standing is always computed live from the ledger.
/// </summary>
public sealed class ArtisanStandingService
{
    private readonly IWalletRepository _wallet;
    private readonly IPlatformSettingsRepository _settings;

    public ArtisanStandingService(IWalletRepository wallet, IPlatformSettingsRepository settings)
    {
        _wallet = wallet;
        _settings = settings;
    }

    /// <summary>Profile ids of every artisan currently past the debt limit.</summary>
    public async Task<IReadOnlyList<Guid>> GetRestrictedProfileIdsAsync(CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        return await _wallet.ListOwnerIdsWithBalanceBelowAsync(
            WalletOwnerType.Artisan, -settings.MaxCommissionDebtNaira, ct);
    }

    /// <summary>True if this artisan is past the debt limit (blocked from new jobs).</summary>
    public async Task<bool> IsRestrictedAsync(Guid artisanProfileId, CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        var balance = await _wallet.GetBalanceAsync(WalletOwnerType.Artisan, artisanProfileId, ct);
        return balance < -settings.MaxCommissionDebtNaira;
    }
}
