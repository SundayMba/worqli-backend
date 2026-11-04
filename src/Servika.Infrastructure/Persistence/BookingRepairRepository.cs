using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of the repairs proposed on jobs.</summary>
public sealed class BookingRepairRepository : IBookingRepairRepository
{
    private readonly ServikaDbContext _db;

    public BookingRepairRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BookingRepair>> ListForBookingAsync(Guid bookingId, CancellationToken ct) =>
        await _db.BookingRepairs.Where(r => r.BookingId == bookingId)
            .OrderBy(r => r.Round).ThenBy(r => r.ProposedAtUtc).ToListAsync(ct);

    public Task<BookingRepair?> FindAsync(Guid bookingId, Guid repairId, CancellationToken ct) =>
        _db.BookingRepairs.AsNoTracking().FirstOrDefaultAsync(r => r.BookingId == bookingId && r.Id == repairId, ct);

    public void Add(BookingRepair repair) => _db.BookingRepairs.Add(repair);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
