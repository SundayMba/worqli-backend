using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>The signed-in artisan's payout history, newest first.</summary>
public sealed class GetArtisanWithdrawalsHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IWithdrawalRepository _withdrawals;

    public GetArtisanWithdrawalsHandler(
        ICatalogueRepository catalogue, IWithdrawalRepository withdrawals)
    {
        _catalogue = catalogue;
        _withdrawals = withdrawals;
    }

    public async Task<IReadOnlyList<WithdrawalDto>> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var items = await _withdrawals.ListForOwnerAsync(WalletOwnerType.Artisan, profile.Id, ct);
        return items.Select(w => w.ToDto()).ToList();
    }
}
