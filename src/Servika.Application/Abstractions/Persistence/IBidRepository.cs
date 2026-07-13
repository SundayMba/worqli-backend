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

    Task<int> SaveChangesAsync(CancellationToken ct);
}
