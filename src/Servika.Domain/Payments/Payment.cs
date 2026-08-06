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

    /// <summary>The booking being paid — null for a commission settlement,
    /// which is money owed by the artisan rather than for a job.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The paying user — the booking's customer, or for a commission
    /// settlement the artisan's own login account.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>What this payment is for (escrow vs commission settlement).</summary>
    public PaymentPurpose Purpose { get; private set; } = PaymentPurpose.BookingEscrow;

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

    /// <summary>When we REQUESTED the refund (ledger reversed, Paystack asked).
    /// The money movement itself settles asynchronously — see the two below.</summary>
    public DateTimeOffset? RefundedAtUtc { get; private set; }

    /// <summary>When the provider CONFIRMED the refund actually reached the
    /// customer (Paystack <c>refund.processed</c> webhook). Null until then.</summary>
    public DateTimeOffset? RefundSettledAtUtc { get; private set; }

    /// <summary>When the provider reported the refund FAILED to process (Paystack
    /// <c>refund.failed</c>). The customer is still owed — this flags it for a
    /// manual retry; the ledger refund is not reversed (the dispute stands).</summary>
    public DateTimeOffset? RefundFailedAtUtc { get; private set; }

    /// <summary>When the artisan's earning + platform commission were released
    /// from escrow into the ledger. Null while the money is still HELD (paid but
    /// the job isn't completed yet). Set once, at completion — this is what makes
    /// the escrow real: the artisan can't withdraw an earning that was never
    /// released, and a pre-completion refund has nothing to claw back.</summary>
    public DateTimeOffset? EarningReleasedAtUtc { get; private set; }

    /// <summary>True once escrow has been released to the split ledger entries.</summary>
    public bool IsEarningReleased => EarningReleasedAtUtc is not null;

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
            Purpose = PaymentPurpose.BookingEscrow,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Starts an artisan's payment of owed cash-job commission. No booking, no
    /// split — a successful settlement simply credits the artisan's ledger.
    /// </summary>
    public static Payment InitiateSettlement(
        Guid artisanUserId,
        Guid artisanProfileId,
        int amountNaira,
        string provider,
        string reference,
        string? authorizationUrl,
        DateTimeOffset now)
    {
        if (amountNaira <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amountNaira));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A gateway reference is required.", nameof(reference));

        return new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = null,
            CustomerId = artisanUserId,
            ArtisanId = artisanProfileId,
            AmountNaira = amountNaira,
            CommissionRate = 0m,
            Provider = provider,
            Reference = reference,
            AuthorizationUrl = authorizationUrl,
            Status = PaymentStatus.Pending,
            Purpose = PaymentPurpose.CommissionSettlement,
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

    /// <summary>Releases the escrow split (platform commission + artisan earning)
    /// into the ledger at job completion. Only a Succeeded payment can release,
    /// and only once — so completion, auto-confirm and a favour-artisan dispute
    /// resolution can all call it safely, and a late duplicate is a no-op.</summary>
    public void MarkEarningReleased(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Succeeded payment can release escrow.");
        EarningReleasedAtUtc ??= now;
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

    /// <summary>Records that the provider confirmed the refund reached the customer
    /// (<c>refund.processed</c>). No-op unless the payment is Refunded (i.e. we
    /// actually requested it); idempotent on repeated webhooks.</summary>
    public void ConfirmRefundSettled(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Refunded) return;
        RefundSettledAtUtc ??= now;
        RefundFailedAtUtc = null; // a later success clears an earlier failure
    }

    /// <summary>Records that the provider failed to process the refund
    /// (<c>refund.failed</c>) so it can be retried. No-op unless Refunded.</summary>
    public void MarkRefundFailed(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Refunded) return;
        if (RefundSettledAtUtc is not null) return; // already landed — ignore
        RefundFailedAtUtc = now;
    }
}
