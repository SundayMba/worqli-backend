using Servika.Domain.Payments;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Append-only wallet ledger. Entries are only ever added; a balance is
/// computed as the sum of one owner's entries.</summary>
public interface IWalletRepository
{
    void Add(WalletTransaction transaction);

    Task<IReadOnlyList<WalletTransaction>> ListForOwnerAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>Every ledger entry, newest first — admin payments analytics (unscoped).</summary>
    Task<IReadOnlyList<WalletTransaction>> ListAllAsync(CancellationToken ct);

    /// <summary>Signed sum of an owner's ledger entries = their current balance.</summary>
    Task<int> GetBalanceAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>Owner ids whose ledger balance is below the threshold — the
    /// artisans past the commission-debt limit (standing enforcement).</summary>
    Task<IReadOnlyList<Guid>> ListOwnerIdsWithBalanceBelowAsync(
        WalletOwnerType ownerType, int threshold, CancellationToken ct);

    /// <summary>Every owner's ledger balance for one owner type, in one grouped
    /// query — for the admin directory's per-artisan standing (no N+1).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ListBalancesAsync(
        WalletOwnerType ownerType, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Opens a transaction holding a lock on one owner's wallet until it is committed or disposed. Two requests
    /// for the same owner then run one after the other, so both cannot spend the same balance.
    /// </summary>
    Task<IWalletLock> LockOwnerAsync(WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);
}

public interface IWalletLock : IAsyncDisposable
{
    /// <summary>Saves pending changes and commits, releasing the lock.</summary>
    Task CommitAsync(CancellationToken ct);
}
