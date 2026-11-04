using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>One price-book booking, reduced to what the price signals need.</summary>
public sealed record PriceBookBookingFact(
    Guid ItemId, BookingStatus Status, BookingPaymentState PaymentState, DateTimeOffset? OffersExhaustedAtUtc);

/// <summary>One offer made on a price-book booking.</summary>
public sealed record PriceBookOfferFact(Guid ItemId, JobOfferStatus Status, string? DeclineReason);

/// <summary>Servika's price book.</summary>
public interface IPriceBookRepository
{
    /// <summary>Active items (read-only), optionally one category and/or one kind, in display order.</summary>
    Task<IReadOnlyList<PriceBookItem>> ListActiveAsync(string? categorySlug, PriceBookItemKind? kind, CancellationToken ct);

    /// <summary>Every item, incl. inactive (read-only), for the console.</summary>
    Task<IReadOnlyList<PriceBookItem>> ListAllAsync(CancellationToken ct);

    Task<PriceBookItem?> FindAsync(Guid id, CancellationToken ct);
    Task<PriceBookItem?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Price-book bookings created since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<PriceBookBookingFact>> ListBookingFactsSinceAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>Offers on price-book bookings created since <paramref name="since"/>.</summary>
    Task<IReadOnlyList<PriceBookOfferFact>> ListOfferFactsSinceAsync(DateTimeOffset since, CancellationToken ct);

    void Add(PriceBookItem item);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
