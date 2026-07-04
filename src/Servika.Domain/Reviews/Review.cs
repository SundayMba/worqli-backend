namespace Servika.Domain.Reviews;

/// <summary>
/// A customer's rating + comment left against a completed booking. Like every
/// Domain entity it knows nothing about the database or the web — only the
/// review's data and the rules that must always hold (1–5 stars, one per booking,
/// immutable once left).
///
/// <para>The reviewer's name (<see cref="CustomerName"/>) and the job's service
/// name (<see cref="ServiceName"/>) are denormalised onto the row so the artisan
/// profile's reviews list renders without joins — the same convention
/// <c>Booking</c> uses for its display fields.</para>
///
/// <para>The "who may review" and "only a completed booking, once" rules are
/// enforced by the use-case handler (it needs the booking's state + ownership);
/// the Domain guards the review's own invariants.</para>
/// </summary>
public sealed class Review
{
    public Guid Id { get; private set; }

    /// <summary>The booking this review is for. Unique — one review per booking.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>The artisan profile being reviewed (same id space as
    /// <c>Booking.ArtisanId</c> — catalogue reference data, no FK).</summary>
    public Guid ArtisanId { get; private set; }

    /// <summary>The customer (a <c>User</c>) who left the review.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Denormalised reviewer name for display, e.g. "Modupe A.".</summary>
    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>Stars, 1–5.</summary>
    public int Rating { get; private set; }

    /// <summary>Optional free-text comment.</summary>
    public string? Comment { get; private set; }

    /// <summary>Denormalised service name of the booking, e.g. "Plumbing".</summary>
    public string ServiceName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core rebuilds rows through this; private so app code can't skip the rules.
    private Review() { }

    /// <summary>
    /// Creates a review. Rating must be 1–5; the comment is optional. Ids and the
    /// display fields are resolved by the caller (from the booking + user) and
    /// passed in, keeping the Domain free of lookups.
    /// </summary>
    public static Review Create(
        Guid bookingId,
        Guid artisanId,
        Guid customerId,
        string customerName,
        int rating,
        string? comment,
        string serviceName,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (artisanId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer is required.", nameof(customerId));
        if (rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        return new Review
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ArtisanId = artisanId,
            CustomerId = customerId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim(),
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            ServiceName = serviceName?.Trim() ?? string.Empty,
            CreatedAt = now,
        };
    }
}
