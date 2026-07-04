namespace Servika.Domain.Disputes;

/// <summary>
/// A customer's complaint about a booking (poor work, no-show, payment problem,
/// unsafe behaviour, …). One dispute freezes its booking in
/// <c>BookingStatus.Disputed</c> until an admin resolves it. The record keeps an
/// audit trail: who raised it and when, who handled it, the decision, and a note.
///
/// <para>Like every Domain entity it holds only data + the rules that must stay
/// true (its status transitions). It knows nothing about the database or the web.</para>
/// </summary>
public sealed class Dispute
{
    public Guid Id { get; private set; }

    /// <summary>The booking this dispute is about.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>The customer (a <c>User</c>) who raised it.</summary>
    public Guid RaisedByUserId { get; private set; }

    /// <summary>Denormalised for join-free admin listing (like Review).</summary>
    public string CustomerName { get; private set; } = string.Empty;
    public string ServiceName { get; private set; } = string.Empty;

    /// <summary>Issue category, e.g. "quality" / "no-show" / "payment" / "unsafe" / "other".</summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>The customer's account of what went wrong.</summary>
    public string Description { get; private set; } = string.Empty;

    public DisputeStatus Status { get; private set; }
    public DisputeResolution Resolution { get; private set; }

    /// <summary>The admin's closing note (why it was decided that way).</summary>
    public string? ResolutionNote { get; private set; }

    /// <summary>The admin (a <c>User</c>) who took it under review / resolved it.</summary>
    public Guid? HandledByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    private Dispute() { }

    /// <summary>Opens a dispute for a booking.</summary>
    public static Dispute Raise(
        Guid bookingId, Guid raisedByUserId, string customerName, string serviceName,
        string category, string description, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (raisedByUserId == Guid.Empty)
            throw new ArgumentException("Raiser is required.", nameof(raisedByUserId));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("An issue category is required.", nameof(category));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Please describe the issue.", nameof(description));

        return new Dispute
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            RaisedByUserId = raisedByUserId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim(),
            ServiceName = serviceName?.Trim() ?? string.Empty,
            Category = category.Trim(),
            Description = description.Trim(),
            Status = DisputeStatus.Open,
            Resolution = DisputeResolution.None,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>An admin acknowledges the dispute and starts investigating.</summary>
    public void MarkUnderReview(Guid adminUserId, DateTimeOffset now)
    {
        if (Status is not DisputeStatus.Open)
            throw new InvalidDisputeStateException(
                $"Only an open dispute can be moved under review (this one is {Status}).");

        Status = DisputeStatus.UnderReview;
        HandledByUserId = adminUserId;
        UpdatedAt = now;
    }

    /// <summary>An admin closes the dispute with a decision.</summary>
    public void Resolve(Guid adminUserId, DisputeResolution resolution, string? note, DateTimeOffset now)
    {
        if (Status is DisputeStatus.Resolved)
            throw new InvalidDisputeStateException("This dispute is already resolved.");
        if (resolution is DisputeResolution.None)
            throw new ArgumentException("A resolution decision is required.", nameof(resolution));

        Status = DisputeStatus.Resolved;
        Resolution = resolution;
        ResolutionNote = note?.Trim();
        HandledByUserId = adminUserId;
        ResolvedAtUtc = now;
        UpdatedAt = now;
    }
}
