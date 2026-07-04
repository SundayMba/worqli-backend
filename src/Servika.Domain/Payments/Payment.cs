namespace Servika.Domain.Payments;

/// <summary>
/// A single payment attempt against a booking. Created <see cref="PaymentStatus.Pending"/>
/// when the customer initializes payment; the gateway webhook later moves it to a
/// terminal state. The commission rate is captured at initialization (from the
/// booking) so the split is fixed regardless of later policy changes — the ledger
/// basis the PRD wants from day one.
///
/// Money never comes from the client: the amount is decided by the server from the
/// booking, and the status is only ever advanced here, never trusted from a callback
/// body until that callback's signature has been verified.
/// </summary>
public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid CustomerId { get; private set; }

    /// <summary>The artisan profile being paid (nullable — open bookings).</summary>
    public Guid? ArtisanId { get; private set; }

    public int AmountNaira { get; private set; }

    /// <summary>Commission fraction (0–1) captured from the booking at init time.</summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>Gateway slug, e.g. "paystack" or "stub".</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Our unique reference handed to the gateway and echoed in the webhook.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Where to send the customer to pay (gateway-hosted), if any.</summary>
    public string? AuthorizationUrl { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public DateTimeOffset? RefundedAtUtc { get; private set; }

    /// <summary>Servika's cut, rounded to whole Naira.</summary>
    public int CommissionNaira =>
        (int)Math.Round(AmountNaira * CommissionRate, MidpointRounding.AwayFromZero);

    /// <summary>What the artisan is owed: amount minus commission.</summary>
    public int ArtisanEarningNaira => AmountNaira - CommissionNaira;

    private Payment() { }

    public static Payment Initiate(
        Guid bookingId,
        Guid customerId,
        Guid? artisanId,
        int amountNaira,
        decimal commissionRate,
        string provider,
        string reference,
        string? authorizationUrl,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking is required.", nameof(bookingId));
        if (amountNaira <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amountNaira));
        if (commissionRate is < 0 or > 1)
            throw new ArgumentException("Commission rate must be between 0 and 1.", nameof(commissionRate));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A gateway reference is required.", nameof(reference));

        return new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            CustomerId = customerId,
            ArtisanId = artisanId,
            AmountNaira = amountNaira,
            CommissionRate = commissionRate,
            Provider = provider,
            Reference = reference,
            AuthorizationUrl = authorizationUrl,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>Marks a successful charge. Idempotent at the callsite via the status
    /// guard — a payment can only leave <see cref="PaymentStatus.Pending"/> once.</summary>
    public void MarkSucceeded(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Pending payment can succeed.");
        Status = PaymentStatus.Succeeded;
        PaidAtUtc = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Pending payment can fail.");
        Status = PaymentStatus.Failed;
        FailedAtUtc = now;
    }

    /// <summary>Reverses a settled payment (e.g. a dispute resolved for the customer).
    /// Only a Succeeded payment can be refunded, and only once.</summary>
    public void MarkRefunded(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Succeeded payment can be refunded.");
        Status = PaymentStatus.Refunded;
        RefundedAtUtc = now;
    }
}
