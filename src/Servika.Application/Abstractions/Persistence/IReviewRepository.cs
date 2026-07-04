using Servika.Domain.Reviews;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for reviews, stated in Domain terms; Infrastructure implements it
/// with EF Core. <see cref="Add"/> only stages the row — nothing hits the database
/// until <see cref="SaveChangesAsync"/>, so a new review and the artisan's updated
/// rating aggregate commit together in one transaction (they share the DbContext).
/// </summary>
public interface IReviewRepository
{
    void Add(Review review);

    /// <summary>True if this booking has already been reviewed (one review per booking).</summary>
    Task<bool> ExistsForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>The review for a booking owned by this customer, or null — powers
    /// the "your review" state on the booking detail screen.</summary>
    Task<Review?> FindForBookingAsync(Guid bookingId, Guid customerId, CancellationToken ct);

    /// <summary>An artisan's reviews, newest first.</summary>
    Task<IReadOnlyList<Review>> ListForArtisanAsync(Guid artisanId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
