using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>The signed-in user's wallet ledger history, newest first.</summary>
public sealed class GetWalletTransactionsHandler
{
    private readonly IWalletRepository _wallet;

    public GetWalletTransactionsHandler(IWalletRepository wallet)
    {
        _wallet = wallet;
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> HandleAsync(
        Guid userId, CancellationToken ct)
    {
        var txns = await _wallet.ListForOwnerAsync(WalletOwnerType.Customer, userId, ct);
        return txns.Select(t => t.ToDto()).ToList();
    }
}
