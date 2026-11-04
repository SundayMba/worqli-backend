using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of Servika's price book.</summary>
public sealed class PriceBookRepository : IPriceBookRepository
{
    private readonly ServikaDbContext _db;

    public PriceBookRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PriceBookItem>> ListActiveAsync(
        string? categorySlug, PriceBookItemKind? kind, CancellationToken ct)
    {
        var query = _db.PriceBookItems.AsNoTracking().Where(i => i.IsActive);
        if (categorySlug is not null) query = query.Where(i => i.CategorySlug == categorySlug);
        if (kind is { } k) query = query.Where(i => i.Kind == k);
        return await query.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PriceBookItem>> ListAllAsync(CancellationToken ct) =>
        await _db.PriceBookItems.AsNoTracking()
            .OrderBy(i => i.CategorySlug).ThenBy(i => i.Kind).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct);

    public Task<PriceBookItem?> FindAsync(Guid id, CancellationToken ct) =>
        _db.PriceBookItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<PriceBookItem?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        _db.PriceBookItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<PriceBookBookingFact>> ListBookingFactsSinceAsync(DateTimeOffset since, CancellationToken ct) =>
        await _db.Bookings.AsNoTracking()
            .Where(b => b.PriceBookItemId != null && b.CreatedAt >= since)
            .Select(b => new PriceBookBookingFact(b.PriceBookItemId!.Value, b.Status, b.PaymentState, b.OffersExhaustedAtUtc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PriceBookOfferFact>> ListOfferFactsSinceAsync(DateTimeOffset since, CancellationToken ct) =>
        await (from o in _db.JobOffers.AsNoTracking()
               join b in _db.Bookings.AsNoTracking() on o.BookingId equals b.Id
               where b.PriceBookItemId != null && b.CreatedAt >= since
               select new PriceBookOfferFact(b.PriceBookItemId!.Value, o.Status, o.DeclineReason))
            .ToListAsync(ct);

    public void Add(PriceBookItem item) => _db.PriceBookItems.Add(item);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
