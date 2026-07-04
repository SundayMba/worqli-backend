using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Reviews;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IReviewRepository"/>. Shares the scoped
/// <see cref="ServikaDbContext"/> with the other repositories, so
/// <see cref="SaveChangesAsync"/> flushes the new review <b>and</b> the artisan's
/// rating aggregate (mutated via the catalogue repository) in one transaction.
/// </summary>
public sealed class ReviewRepository : IReviewRepository
{
    private readonly ServikaDbContext _db;

    public ReviewRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Review review) => _db.Reviews.Add(review);

    public Task<bool> ExistsForBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.Reviews.AnyAsync(r => r.BookingId == bookingId, ct);

    public Task<Review?> FindForBookingAsync(Guid bookingId, Guid customerId, CancellationToken ct) =>
        _db.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.BookingId == bookingId && r.CustomerId == customerId, ct);

    public async Task<IReadOnlyList<Review>> ListForArtisanAsync(Guid artisanId, CancellationToken ct) =>
        await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ArtisanId == artisanId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
