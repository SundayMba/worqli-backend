using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Disputes;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IDisputeRepository"/>. Shares the scoped
/// <see cref="ServikaDbContext"/> with the booking repository, so a dispute and the
/// booking state-change it triggers commit in one transaction.
/// </summary>
public sealed class DisputeRepository : IDisputeRepository
{
    private readonly ServikaDbContext _db;

    public DisputeRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Dispute dispute) => _db.Disputes.Add(dispute);

    public Task<bool> HasOpenForBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.Disputes.AnyAsync(
            d => d.BookingId == bookingId && d.Status != DisputeStatus.Resolved, ct);

    public Task<Dispute?> FindForBookingAsync(Guid bookingId, Guid raisedByUserId, CancellationToken ct) =>
        _db.Disputes
            .AsNoTracking()
            .Where(d => d.BookingId == bookingId && d.RaisedByUserId == raisedByUserId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    // Tracked so the admin's status change persists on SaveChanges.
    public Task<Dispute?> FindByIdForUpdateAsync(Guid id, CancellationToken ct) =>
        _db.Disputes.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Dispute?> FindByBookingIdAsync(Guid bookingId, CancellationToken ct) =>
        _db.Disputes
            .AsNoTracking()
            .Where(d => d.BookingId == bookingId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<Dispute?> FindByBookingIdForUpdateAsync(Guid bookingId, CancellationToken ct) =>
        _db.Disputes
            .Where(d => d.BookingId == bookingId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Dispute>> ListAllAsync(DisputeStatus? status, CancellationToken ct)
    {
        var query = _db.Disputes.AsNoTracking().AsQueryable();
        if (status is { } s)
            query = query.Where(d => d.Status == s);

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
