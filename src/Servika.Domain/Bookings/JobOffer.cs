namespace Servika.Domain.Bookings;

/// <summary>Where an offer came from: Servika's rotation, or a customer who asked for this artisan.</summary>
public enum JobOfferSource { Rotation = 0, Direct = 1 }

public enum JobOfferStatus
{
    /// <summary>Live: the artisan may accept or decline until it expires.</summary>
    Offered = 0,
    Accepted = 1,
    Declined = 2,
    /// <summary>The window passed with no answer; the job moved on.</summary>
    Expired = 3,
    /// <summary>Servika took it back (the customer cancelled, asked for others, or the job was reassigned).</summary>
    Withdrawn = 4,
}

/// <summary>
/// One job offered to one artisan for a window of time (Business Model v1.4, section 6:
/// Servika assigns the job; nobody browses or bids). Every offer is kept, so rotation can skip
/// artisans who already had their turn and ops can see who declined what.
/// </summary>
public sealed class JobOffer
{
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    /// <summary>The artisan profile (what bookings reference) and their login (what notifications need).</summary>
    public Guid ArtisanProfileId { get; private set; }
    public Guid ArtisanUserId { get; private set; }
    /// <summary>1 for the first artisan asked, 2 for the next, and so on.</summary>
    public int Round { get; private set; }
    public JobOfferSource Source { get; private set; }
    public JobOfferStatus Status { get; private set; }
    public DateTimeOffset OfferedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    /// <summary>What the artisan said when declining (a short reason chip), if anything.</summary>
    public string? DeclineReason { get; private set; }

    private JobOffer() { }

    public static JobOffer Create(
        Guid bookingId, Guid artisanProfileId, Guid artisanUserId, int round, JobOfferSource source,
        DateTimeOffset now, DateTimeOffset expiresAtUtc)
    {
        if (bookingId == Guid.Empty) throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (artisanProfileId == Guid.Empty) throw new ArgumentException("Artisan is required.", nameof(artisanProfileId));
        if (artisanUserId == Guid.Empty) throw new ArgumentException("Artisan account is required.", nameof(artisanUserId));
        if (round < 1) throw new ArgumentException("Round starts at 1.", nameof(round));
        if (expiresAtUtc <= now) throw new ArgumentException("An offer must expire after it is made.", nameof(expiresAtUtc));
        return new JobOffer
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ArtisanProfileId = artisanProfileId,
            ArtisanUserId = artisanUserId,
            Round = round,
            Source = source,
            Status = JobOfferStatus.Offered,
            OfferedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    public bool IsLive(DateTimeOffset now) => Status == JobOfferStatus.Offered && ExpiresAtUtc > now;

    public void Accept(DateTimeOffset now)
    {
        if (!IsLive(now)) throw new InvalidBookingStateException("This offer is no longer open.");
        Status = JobOfferStatus.Accepted;
        RespondedAtUtc = now;
    }

    public void Decline(DateTimeOffset now, string? reason)
    {
        if (!IsLive(now)) throw new InvalidBookingStateException("This offer is no longer open.");
        Status = JobOfferStatus.Declined;
        RespondedAtUtc = now;
        DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>The window passed with no answer. Idempotent.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (Status != JobOfferStatus.Offered) return;
        Status = JobOfferStatus.Expired;
        RespondedAtUtc = now;
    }

    /// <summary>Servika takes the offer back. Idempotent.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        if (Status != JobOfferStatus.Offered) return;
        Status = JobOfferStatus.Withdrawn;
        RespondedAtUtc = now;
    }
}
