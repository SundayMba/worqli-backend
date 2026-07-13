namespace Servika.Domain.Bookings;

/// <summary>
/// An artisan's price offer on an open <see cref="AssessmentMode.RemoteQuote"/>
/// request. One bid per (booking, artisan) — re-bidding updates the amount.
/// The customer reviews the bidders and accepts one; acceptance assigns the
/// booking to that artisan at the offered price.
/// </summary>
public sealed class Bid
{
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }

    /// <summary>The bidding artisan's marketplace profile id.</summary>
    public Guid ArtisanId { get; private set; }

    /// <summary>The artisan's login account — who to notify on acceptance.</summary>
    public Guid ArtisanUserId { get; private set; }

    /// <summary>Denormalised for join-free listing to the customer.</summary>
    public string ArtisanName { get; private set; } = string.Empty;

    /// <summary>The offered price for the whole job, in Naira.</summary>
    public int AmountNaira { get; private set; }

    /// <summary>Optional: what the artisan says they'd need to fix the job
    /// (materials, parts). Purely informational for the customer.</summary>
    public string? MaterialsNote { get; private set; }

    public BidStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Bid() { }

    public const int MaxMaterialsNoteLength = 500;

    public static Bid Place(
        Guid bookingId,
        Guid artisanId,
        Guid artisanUserId,
        string artisanName,
        int amountNaira,
        string? materialsNote,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (artisanId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanId));

        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ArtisanId = artisanId,
            ArtisanUserId = artisanUserId,
            ArtisanName = artisanName?.Trim() ?? string.Empty,
            Status = BidStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        bid.Revise(amountNaira, materialsNote, now);
        return bid;
    }

    /// <summary>Updates the offer (re-bidding). Only an Active bid can change.</summary>
    public void Revise(int amountNaira, string? materialsNote, DateTimeOffset now)
    {
        if (Status is not BidStatus.Active)
            throw new InvalidBookingStateException("This bid is closed and can't be changed.");
        if (amountNaira <= 0)
            throw new ArgumentException("Offer a price greater than zero.", nameof(amountNaira));

        var note = materialsNote?.Trim();
        if (note is { Length: > MaxMaterialsNoteLength })
            note = note[..MaxMaterialsNoteLength];

        AmountNaira = amountNaira;
        MaterialsNote = string.IsNullOrEmpty(note) ? null : note;
        UpdatedAt = now;
    }

    /// <summary>The customer chose this bid.</summary>
    public void MarkAccepted(DateTimeOffset now)
    {
        if (Status is not BidStatus.Active)
            throw new InvalidBookingStateException("This bid is no longer active.");
        Status = BidStatus.Accepted;
        UpdatedAt = now;
    }

    /// <summary>Another bid won (or the request closed) — this one is done.</summary>
    public void MarkClosed(DateTimeOffset now)
    {
        if (Status is BidStatus.Active)
        {
            Status = BidStatus.Closed;
            UpdatedAt = now;
        }
    }
}

/// <summary>Lifecycle of a bid. Stored as a string.</summary>
public enum BidStatus
{
    /// <summary>Standing offer the customer can accept.</summary>
    Active = 1,

    /// <summary>The customer accepted this bid — it became the booking's price.</summary>
    Accepted = 2,

    /// <summary>The request was filled by another bid or closed.</summary>
    Closed = 3,
}
