namespace Servika.Domain.Bookings;

public enum BookingRepairStatus
{
    /// <summary>Sent by the artisan, waiting for the customer.</summary>
    Proposed = 0,
    Approved = 1,
    Declined = 2,
}

/// <summary>
/// One repair an artisan found on a job and proposed from Servika's add-on list (Business Model v1.4,
/// section 7: "the artisan only picks which items apply, with a photo as proof"). The labour price is
/// Servika's, copied from the price book when proposed so a later price change never moves a job in
/// flight. Parts are itemised beside it and the receipt is shown at completion. The customer approves or
/// declines each repair in the app before any of it is done.
/// </summary>
public sealed class BookingRepair
{
    public const int MaxPartsNaira = 2_000_000;
    public const int MaxPartsNoteLength = 160;

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid PriceBookItemId { get; private set; }
    /// <summary>Which proposal this repair came in (1 = the first visit's list).</summary>
    public int Round { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int LabourNaira { get; private set; }
    public int PartsNaira { get; private set; }
    public string? PartsNote { get; private set; }
    /// <summary>The photo of the fault this repair fixes (storage key). Required: it is the proof.</summary>
    public string PhotoKey { get; private set; } = string.Empty;
    public BookingRepairStatus Status { get; private set; }
    public DateTimeOffset ProposedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public int TotalNaira => LabourNaira + PartsNaira;

    private BookingRepair() { }

    public static BookingRepair Propose(
        Guid bookingId, Guid priceBookItemId, int round, string name, int labourNaira, int partsNaira,
        string? partsNote, string photoKey, DateTimeOffset now)
    {
        if (labourNaira <= 0) throw new ArgumentException("A repair from the list always has a labour price.");
        if (partsNaira is < 0 or > MaxPartsNaira)
            throw new ArgumentException($"Parts must be between ₦0 and ₦{MaxPartsNaira:N0}.");
        var note = string.IsNullOrWhiteSpace(partsNote) ? null : partsNote.Trim();
        if (note is { Length: > MaxPartsNoteLength })
            throw new ArgumentException($"Keep the parts note under {MaxPartsNoteLength} characters.");
        if (partsNaira > 0 && note is null)
            throw new ArgumentException($"Say which part costs ₦{partsNaira:N0} for \"{name}\".");
        if (string.IsNullOrWhiteSpace(photoKey))
            throw new ArgumentException($"Add a photo of the fault for \"{name}\".");

        return new BookingRepair
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            PriceBookItemId = priceBookItemId,
            Round = round,
            Name = name,
            LabourNaira = labourNaira,
            PartsNaira = partsNaira,
            PartsNote = note,
            PhotoKey = photoKey,
            Status = BookingRepairStatus.Proposed,
            ProposedAtUtc = now,
        };
    }

    public void Decide(bool approve, DateTimeOffset now)
    {
        if (Status != BookingRepairStatus.Proposed)
            throw new InvalidBookingStateException($"\"{Name}\" was already {Status.ToString().ToLowerInvariant()}.");
        Status = approve ? BookingRepairStatus.Approved : BookingRepairStatus.Declined;
        DecidedAtUtc = now;
    }
}
