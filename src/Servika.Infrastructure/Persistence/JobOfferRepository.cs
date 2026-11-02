using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.Infrastructure.Persistence;

/// <inheritdoc cref="IJobOfferRepository"/>
public sealed class JobOfferRepository : IJobOfferRepository
{
    private readonly ServikaDbContext _db;

    public JobOfferRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(JobOffer offer) => _db.JobOffers.Add(offer);

    public async Task<IReadOnlyList<JobOffer>> ListLiveForBookingAsync(Guid bookingId, CancellationToken ct) =>
        await _db.JobOffers
            .Where(o => o.BookingId == bookingId && o.Status == JobOfferStatus.Offered)
            .ToListAsync(ct);

    public Task<JobOffer?> FindLiveForArtisanAsync(Guid bookingId, Guid artisanProfileId, CancellationToken ct) =>
        _db.JobOffers
            .Where(o => o.BookingId == bookingId && o.ArtisanProfileId == artisanProfileId && o.Status == JobOfferStatus.Offered)
            .OrderByDescending(o => o.OfferedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListOfferedProfileIdsAsync(Guid bookingId, CancellationToken ct) =>
        await _db.JobOffers.AsNoTracking()
            .Where(o => o.BookingId == bookingId)
            .Select(o => o.ArtisanProfileId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobOffer>> ListLiveForArtisanAsync(Guid artisanProfileId, DateTimeOffset now, CancellationToken ct) =>
        await _db.JobOffers.AsNoTracking()
            .Where(o => o.ArtisanProfileId == artisanProfileId && o.Status == JobOfferStatus.Offered && o.ExpiresAtUtc > now)
            .OrderByDescending(o => o.OfferedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobOffer>> ListExpiredLiveAsync(DateTimeOffset now, CancellationToken ct) =>
        await _db.JobOffers
            .Where(o => o.Status == JobOfferStatus.Offered && o.ExpiresAtUtc <= now)
            .OrderBy(o => o.ExpiresAtUtc)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
