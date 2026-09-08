namespace Servika.Domain.Bookings;

/// <summary>
/// The artisan's rating of a customer after a job (design 61). Private to
/// Servika: it never shows on the customer's profile; admins read it when a
/// dispute or a pattern comes up. One per booking.
/// </summary>
public sealed class CustomerRating
{
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ArtisanUserId { get; private set; }
    /// <summary>1–5: "would you work for them again?"</summary>
    public int Stars { get; private set; }
    /// <summary>Tags in the artisan's terms ("Paid without argument", …), max 6.</summary>
    public List<string> Tags { get; private set; } = new();
    /// <summary>A note only Servika reads.</summary>
    public string? PrivateNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CustomerRating() { }

    public static CustomerRating Create(
        Guid bookingId, Guid customerId, Guid artisanUserId, int stars,
        IEnumerable<string>? tags, string? privateNote, DateTimeOffset now)
    {
        if (stars is < 1 or > 5)
            throw new ArgumentException("Stars must be between 1 and 5.", nameof(stars));
        return new CustomerRating
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            CustomerId = customerId,
            ArtisanUserId = artisanUserId,
            Stars = stars,
            Tags = (tags ?? Enumerable.Empty<string>())
                .Select(t => (t ?? string.Empty).Trim())
                .Where(t => t.Length > 0)
                .Distinct()
                .Take(6)
                .ToList(),
            PrivateNote = string.IsNullOrWhiteSpace(privateNote) ? null : privateNote.Trim(),
            CreatedAt = now,
        };
    }
}
