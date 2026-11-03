using Servika.Domain.Bookings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for bids on open RemoteQuote requests.</summary>
public interface IBidRepository
{
    void Add(Bid bid);

    /// <summary>This artisan's bid on a booking (one per pair), tracked. Null if none.</summary>
    Task<Bid?> FindForArtisanAsync(Guid bookingId, Guid artisanProfileId, CancellationToken ct);

    /// <summary>All bids on a booking, tracked, cheapest first.</summary>
    Task<IReadOnlyList<Bid>> ListForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>Active-bid count per booking, for list badges.</summary>
    Task<int> CountActiveForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>Active custom quotes waiting for ops (tracked), oldest first.</summary>
    Task<IReadOnlyList<Bid>> ListAwaitingReviewAsync(CancellationToken ct);

    /// <summary>Tracked fetch by id, or null.</summary>
    Task<Bid?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
