using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// The signed-in user's wallet balance, computed from the append-only ledger
/// (never trusted from the client). For customers this is their net of payments
/// and refunds; artisan-wallet views arrive with the artisan app.
/// </summary>
public sealed class GetWalletHandler
{
    private readonly IWalletRepository _wallet;

    public GetWalletHandler(IWalletRepository wallet)
    {
        _wallet = wallet;
    }

    public async Task<WalletDto> HandleAsync(Guid userId, CancellationToken ct)
    {
        var balance = await _wallet.GetBalanceAsync(WalletOwnerType.Customer, userId, ct);
        return new WalletDto(balance, "NGN");
    }
}
