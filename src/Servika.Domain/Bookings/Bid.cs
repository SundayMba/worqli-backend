namespace Servika.Domain.Bookings;

/// <summary>
/// An artisan's price offer on a request — a competing bid on an open
/// <see cref="AssessmentMode.RemoteQuote"/> broadcast, or the pre-selected
/// artisan's quote on a direct Pending booking. One bid per (booking, artisan)
/// — re-bidding updates the amount. The customer accepts one; acceptance makes
/// it the booking's agreed price (and assigns the artisan, for broadcasts).
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

    /// <summary>The offered price for the whole job, in Naira — always
    /// <see cref="WorkmanshipNaira"/> + the materials total. What the customer pays.</summary>
    public int AmountNaira { get; private set; }

    /// <summary>The labour part of the quote. This is the part the two sides
    /// bargain over; materials are priced per item, not haggled.</summary>
    public int WorkmanshipNaira { get; private set; }

    /// <summary>Itemised materials/parts the artisan will buy for the job. Empty
    /// when the quote is labour-only. Their sum is <see cref="MaterialsNaira"/>.</summary>
    public List<BidMaterialLine> Materials { get; private set; } = new();

    public int MaterialsNaira => Materials.Sum(m => m.TotalNaira);

    /// <summary>Optional: what the artisan says they'd need to fix the job
    /// (materials, parts). Purely informational for the customer.</summary>
    public string? MaterialsNote { get; private set; }

    public BidStatus Status { get; private set; }

    /// <summary>The customer's standing counter-offer on WORKMANSHIP (materials are
    /// priced per item and not haggled). Null when there is none pending. The artisan
    /// accepts it (deal), declines it, or answers with a revised quote (which clears it).</summary>
    public int? PendingCounterNaira { get; private set; }
    public string? PendingCounterNote { get; private set; }

    /// <summary>How many counter-offers the customer has made on this bid. Capped so the
    /// haggle ends: after <see cref="MaxCounterRounds"/> the customer can only accept or walk.</summary>
    public int CounterRounds { get; private set; }

    public const int MaxCounterRounds = 3;
    public const int MaxCounterNoteLength = 200;
    public bool HasPendingCounter => PendingCounterNaira is not null;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Bid() { }

    public const int MaxMaterialsNoteLength = 500;

    public static Bid Place(
        Guid bookingId,
        Guid artisanId,
        Guid artisanUserId,
        string artisanName,
        int workmanshipNaira,
        IReadOnlyList<BidMaterialLine> materials,
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
        bid.Revise(workmanshipNaira, materials, materialsNote, now);
        return bid;
    }

    /// <summary>Updates the offer (re-bidding). Only an Active bid can change.
    /// The total is derived: workmanship + Σ materials, and must be > 0.</summary>
    public void Revise(
        int workmanshipNaira, IReadOnlyList<BidMaterialLine> materials,
        string? materialsNote, DateTimeOffset now)
    {
        if (Status is not BidStatus.Active)
            throw new InvalidBookingStateException("This bid is closed and can't be changed.");
        if (workmanshipNaira < 0)
            throw new ArgumentException("Workmanship can't be negative.", nameof(workmanshipNaira));
        if (materials.Count > BidMaterialLine.MaxLines)
            throw new ArgumentException(
                $"At most {BidMaterialLine.MaxLines} material lines.", nameof(materials));
        var lines = materials.ToList();
        var amountNaira = workmanshipNaira + lines.Sum(m => m.TotalNaira);
        if (amountNaira <= 0)
            throw new ArgumentException("Offer a price greater than zero.", nameof(workmanshipNaira));

        var note = materialsNote?.Trim();
        if (note is { Length: > MaxMaterialsNoteLength })
            note = note[..MaxMaterialsNoteLength];

        WorkmanshipNaira = workmanshipNaira;
        Materials = lines;
        AmountNaira = amountNaira;
        MaterialsNote = string.IsNullOrEmpty(note) ? null : note;
        // A new price from the artisan answers any open counter-offer.
        PendingCounterNaira = null;
        PendingCounterNote = null;
        UpdatedAt = now;
    }

    /// <summary>The customer proposes a different workmanship price (inDrive-style).</summary>
    public void Counter(int workmanshipNaira, string? note, DateTimeOffset now)
    {
        if (Status is not BidStatus.Active)
            throw new InvalidBookingStateException("This offer is no longer open to negotiation.");
        if (CounterRounds >= MaxCounterRounds)
            throw new InvalidBookingStateException(
                $"You've made {MaxCounterRounds} offers on this quote. Accept it, or wait for the artisan's price.");
        if (workmanshipNaira < 0 || workmanshipNaira + MaterialsNaira <= 0)
            throw new ArgumentException("Offer a workmanship price greater than zero.", nameof(workmanshipNaira));
        if (workmanshipNaira == WorkmanshipNaira)
            throw new ArgumentException("That is already the artisan's price. Accept the offer instead.", nameof(workmanshipNaira));

        var n = note?.Trim();
        if (n is { Length: > MaxCounterNoteLength }) n = n[..MaxCounterNoteLength];

        PendingCounterNaira = workmanshipNaira;
        PendingCounterNote = string.IsNullOrEmpty(n) ? null : n;
        CounterRounds++;
        UpdatedAt = now;
    }

    /// <summary>The artisan agrees to the customer's counter: the quote becomes that
    /// price (materials unchanged). The caller then accepts the bid on the booking.</summary>
    public void AcceptCounter(DateTimeOffset now)
    {
        if (PendingCounterNaira is not { } counter)
            throw new InvalidBookingStateException("There is no customer offer to accept.");
        Revise(counter, Materials, MaterialsNote, now); // also clears the pending counter
    }

    /// <summary>The artisan says no to the counter; their quote stands.</summary>
    public void DeclineCounter(DateTimeOffset now)
    {
        if (PendingCounterNaira is null)
            throw new InvalidBookingStateException("There is no customer offer to decline.");
        PendingCounterNaira = null;
        PendingCounterNote = null;
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
