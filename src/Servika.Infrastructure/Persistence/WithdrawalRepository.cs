using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Payments;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IWithdrawalRepository"/>. Shares the scoped
/// <see cref="ServikaDbContext"/> with the wallet repository, so a withdrawal and
/// the ledger entries reserving/reversing its funds commit in one transaction.
/// </summary>
public sealed class WithdrawalRepository : IWithdrawalRepository
{
    private readonly ServikaDbContext _db;

    public WithdrawalRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Withdrawal withdrawal) => _db.Withdrawals.Add(withdrawal);

    public async Task<IReadOnlyList<Withdrawal>> ListForOwnerAsync(
        WalletOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        await _db.Withdrawals
            .AsNoTracking()
            .Where(w => w.OwnerType == ownerType && w.OwnerId == ownerId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Withdrawal>> ListAllAsync(CancellationToken ct) =>
        await _db.Withdrawals
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
