using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IBookingRepository"/>. Reads are always
/// filtered by <c>CustomerId</c> so the data layer itself can never return another
/// customer's bookings. Writes are tracked (so <c>Cancel</c> mutations persist) and
/// committed via <see cref="SaveChangesAsync"/>.
/// </summary>
public sealed class BookingRepository : IBookingRepository
{
    private readonly ServikaDbContext _db;

    public BookingRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Booking booking) => _db.Bookings.Add(booking);

    public async Task<IReadOnlyList<Booking>> ListForCustomerAsync(
        Guid customerId, BookingStatus? status, CancellationToken ct)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == customerId);

        if (status is { } s)
            query = query.Where(b => b.Status == s);

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<Booking?> FindForCustomerAsync(Guid id, Guid customerId, CancellationToken ct) =>
        _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == customerId, ct);

    public async Task<IReadOnlyList<Booking>> ListForArtisanAsync(
        Guid artisanProfileId, BookingStatus? status, CancellationToken ct)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.ArtisanId == artisanProfileId);

        if (status is { } s)
            query = query.Where(b => b.Status == s);

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    // Tracked (not AsNoTracking) so the artisan's state-machine mutations persist.
    public Task<Booking?> FindForArtisanAsync(Guid id, Guid artisanProfileId, CancellationToken ct) =>
        _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.ArtisanId == artisanProfileId, ct);

    public Task<Booking?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
