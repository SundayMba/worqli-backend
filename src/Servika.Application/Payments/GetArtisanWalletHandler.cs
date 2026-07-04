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

    public GetArtisanWalletHandler(ICatalogueRepository catalogue, IWalletRepository wallet)
    {
        _catalogue = catalogue;
        _wallet = wallet;
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
        var totalWithdrawn = totalEarned - available;

        return new ArtisanWalletDto(available, totalEarned, totalWithdrawn, "NGN");
    }
}
