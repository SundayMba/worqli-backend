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

    public async Task<IReadOnlyList<Booking>> ListOpenInCategoriesAsync(
        IReadOnlyCollection<string> categorySlugs, CancellationToken ct)
    {
        if (categorySlugs.Count == 0) return Array.Empty<Booking>();

        return await _db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Open && categorySlugs.Contains(b.CategorySlug))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    // Single guarded UPDATE: only rows still Open are touched, so exactly one of many
    // racing artisans updates a row (rows > 0); the losers see 0 and get a 409. Runs
    // immediately against the DB — no reliance on the change tracker / SaveChanges.
    public async Task<bool> TryClaimAsync(
        Guid bookingId, Guid artisanProfileId, string artisanName, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await _db.Bookings
            .Where(b => b.Id == bookingId && b.Status == BookingStatus.Open)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.ArtisanId, artisanProfileId)
                .SetProperty(b => b.ArtisanName, artisanName)
                .SetProperty(b => b.Status, BookingStatus.Accepted)
                .SetProperty(b => b.AcceptedAtUtc, now), ct);
        return rows > 0;
    }

    public Task<Booking?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Booking?> FindByIdReadOnlyAsync(Guid id, CancellationToken ct) =>
        _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Booking>> ListAllAsync(BookingStatus? status, CancellationToken ct)
    {
        var query = _db.Bookings.AsNoTracking().AsQueryable();
        if (status is { } s)
            query = query.Where(b => b.Status == s);
        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
    }

    // Tracked so the auto-confirm sweep's ConfirmCompletion persists.
    public async Task<IReadOnlyList<Booking>> ListAwaitingConfirmationBeforeAsync(
        DateTimeOffset cutoffUtc, CancellationToken ct) =>
        await _db.Bookings
            .Where(b => b.Status == BookingStatus.AwaitingConfirmation
                        && b.WorkSubmittedAtUtc != null
                        && b.WorkSubmittedAtUtc < cutoffUtc)
            .ToListAsync(ct);

    public Task<int> CountCompletedForCustomerAsync(Guid customerId, CancellationToken ct) =>
        _db.Bookings.CountAsync(b => b.CustomerId == customerId && b.Status == BookingStatus.Completed, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
