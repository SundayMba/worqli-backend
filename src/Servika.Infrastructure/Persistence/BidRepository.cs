using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.Infrastructure.Persistence;

/// <inheritdoc cref="IBidRepository"/>
public sealed class BidRepository : IBidRepository
{
    private readonly ServikaDbContext _db;

    public BidRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Bid bid) => _db.Bids.Add(bid);

    public Task<Bid?> FindForArtisanAsync(Guid bookingId, Guid artisanProfileId, CancellationToken ct) =>
        _db.Bids.FirstOrDefaultAsync(
            b => b.BookingId == bookingId && b.ArtisanId == artisanProfileId, ct);

    public async Task<IReadOnlyList<Bid>> ListForBookingAsync(Guid bookingId, CancellationToken ct) =>
        await _db.Bids
            .Where(b => b.BookingId == bookingId)
            .OrderBy(b => b.AmountNaira)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public Task<int> CountActiveForBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.Bids.CountAsync(
            b => b.BookingId == bookingId && b.Status == BidStatus.Active, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
