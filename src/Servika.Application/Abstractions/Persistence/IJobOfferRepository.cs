using Servika.Domain.Bookings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for job offers (one job, one artisan, one window). Shares the booking DbContext,
/// so a handler saves the booking and its offers in one transaction.</summary>
public interface IJobOfferRepository
{
    void Add(JobOffer offer);

    /// <summary>Every offer still Offered on this booking (tracked). Normally one.</summary>
    Task<IReadOnlyList<JobOffer>> ListLiveForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>This artisan's Offered offer on this booking (tracked), or null.</summary>
    Task<JobOffer?> FindLiveForArtisanAsync(Guid bookingId, Guid artisanProfileId, CancellationToken ct);

    /// <summary>Every artisan profile ever offered this booking, whatever they answered.</summary>
    Task<IReadOnlyList<Guid>> ListOfferedProfileIdsAsync(Guid bookingId, CancellationToken ct);

    /// <summary>The artisan's Offered offers that have not expired, newest first (read-only).</summary>
    Task<IReadOnlyList<JobOffer>> ListLiveForArtisanAsync(Guid artisanProfileId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Offered offers whose window has passed (tracked), for the sweep.</summary>
    Task<IReadOnlyList<JobOffer>> ListExpiredLiveAsync(DateTimeOffset now, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
