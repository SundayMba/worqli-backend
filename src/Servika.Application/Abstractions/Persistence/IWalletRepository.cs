using Servika.Domain.Payments;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Append-only wallet ledger. Entries are only ever added; a balance is
/// computed as the sum of one owner's entries.</summary>
public interface IWalletRepository
{
    void Add(WalletTransaction transaction);

    Task<IReadOnlyList<WalletTransaction>> ListForOwnerAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>Signed sum of an owner's ledger entries = their current balance.</summary>
    Task<int> GetBalanceAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
