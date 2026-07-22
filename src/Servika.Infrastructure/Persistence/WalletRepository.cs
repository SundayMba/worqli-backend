using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Payments;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IWalletRepository"/>. The ledger is
/// append-only — only <c>Add</c>; balances are summed in the database.</summary>
public sealed class WalletRepository : IWalletRepository
{
    private readonly ServikaDbContext _db;

    public WalletRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(WalletTransaction transaction) => _db.WalletTransactions.Add(transaction);

    public async Task<IReadOnlyList<WalletTransaction>> ListForOwnerAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.OwnerType == ownerType && t.OwnerId == ownerId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WalletTransaction>> ListAllAsync(CancellationToken ct) =>
        await _db.WalletTransactions
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<int> GetBalanceAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        await _db.WalletTransactions
            .Where(t => t.OwnerType == ownerType && t.OwnerId == ownerId)
            .SumAsync(t => (int?)t.AmountNaira, ct) ?? 0;

    public async Task<IReadOnlyList<Guid>> ListOwnerIdsWithBalanceBelowAsync(
        WalletOwnerType ownerType, int threshold, CancellationToken ct) =>
        await _db.WalletTransactions
            .Where(t => t.OwnerType == ownerType)
            .GroupBy(t => t.OwnerId)
            .Where(g => g.Sum(t => t.AmountNaira) < threshold)
            .Select(g => g.Key)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> ListBalancesAsync(
        WalletOwnerType ownerType, CancellationToken ct) =>
        await _db.WalletTransactions
            .Where(t => t.OwnerType == ownerType)
            .GroupBy(t => t.OwnerId)
            .Select(g => new { OwnerId = g.Key, Balance = g.Sum(t => t.AmountNaira) })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Balance, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
