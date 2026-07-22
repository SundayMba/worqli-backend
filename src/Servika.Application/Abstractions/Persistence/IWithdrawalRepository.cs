using Servika.Domain.Payments;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for artisan payout requests. Shares the scoped
/// <c>ServikaDbContext</c>, so a withdrawal and the wallet ledger entries that
/// reserve/reverse its funds flush in one transaction.
/// </summary>
public interface IWithdrawalRepository
{
    void Add(Withdrawal withdrawal);

    /// <summary>A ledger owner's payout history, newest first.</summary>
    Task<IReadOnlyList<Withdrawal>> ListForOwnerAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>Every payout, newest first — admin payout summary (unscoped).</summary>
    Task<IReadOnlyList<Withdrawal>> ListAllAsync(CancellationToken ct);

    /// <summary>Tracked lookup by id (unscoped) — used by the transfer webhook to
    /// settle a Pending payout.</summary>
    Task<Withdrawal?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
