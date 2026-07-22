using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// The signed-in artisan's earnings summary, computed from the append-only ledger.
/// The artisan is resolved to their profile (user → profile), then the balance is
/// the sum of that profile's ledger entries. Available = earnings − payouts;
/// withdrawn = earnings − available (so a reversed/failed payout nets to zero).
/// </summary>
public sealed class GetArtisanWalletHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IWalletRepository _wallet;
    private readonly IPlatformSettingsRepository _settings;

    public GetArtisanWalletHandler(
        ICatalogueRepository catalogue, IWalletRepository wallet, IPlatformSettingsRepository settings)
    {
        _catalogue = catalogue;
        _wallet = wallet;
        _settings = settings;
    }

    public async Task<ArtisanWalletDto> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var entries = await _wallet.ListForOwnerAsync(WalletOwnerType.Artisan, profile.Id, ct);

        var available = entries.Sum(e => e.AmountNaira);
        var totalEarned = entries
            .Where(e => e.Type == WalletTransactionType.ArtisanEarning)
            .Sum(e => e.AmountNaira);
        // Net cash-job commission position (negative while fees are outstanding);
        // fold it back so it doesn't masquerade as a withdrawal.
        var commissionNet = entries
            .Where(e => e.Type is WalletTransactionType.CommissionDue
                or WalletTransactionType.CommissionSettlement)
            .Sum(e => e.AmountNaira);
        var totalWithdrawn = totalEarned - available + commissionNet;

        // Owed = the part of the debt earnings haven't absorbed (auto-netting):
        // only a negative overall balance is actually outstanding.
        var owed = Math.Max(0, -available);
        var settings = await _settings.GetOrCreateAsync(ct);
        var restricted = available < -settings.MaxCommissionDebtNaira;

        return new ArtisanWalletDto(
            Math.Max(0, available), totalEarned, totalWithdrawn, "NGN", owed, restricted);
    }
}
