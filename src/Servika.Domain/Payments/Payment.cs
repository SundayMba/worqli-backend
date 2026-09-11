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

    /// <summary>The payment fee charged to the customer ON TOP of <see cref="AmountNaira"/>
    /// (0 while Servika absorbs fees). Never part of the escrow or the artisan's earning.</summary>
    public int ServiceFeeNaira { get; private set; }

    /// <summary>What the customer's card was actually charged: price + service fee.</summary>
    public int ChargedNaira => AmountNaira + ServiceFeeNaira;

    /// <summary>What the gateway reported it kept (whole Naira), once the webhook told us.</summary>
    public int? GatewayFeeNaira { get; private set; }

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

    /// <summary>How much was refunded to the customer. Equals <see cref="AmountNaira"/>
    /// for a full refund; less for a partial (the artisan kept the remainder). 0 until
    /// a refund is requested.</summary>
    public int RefundedAmountNaira { get; private set; }

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
        DateTimeOffset now,
        int serviceFeeNaira = 0)
    {
        if (serviceFeeNaira < 0)
            throw new ArgumentException("Service fee can't be negative.", nameof(serviceFeeNaira));
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
            ServiceFeeNaira = serviceFeeNaira,
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

    /// <summary>Records the gateway's own fee as reported on the webhook.</summary>
    public void RecordGatewayFee(int feeNaira)
    {
        if (feeNaira < 0) return;
        GatewayFeeNaira = feeNaira;
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

    /// <summary>True once any refund (full or partial) has been requested on this payment.</summary>
    public bool IsRefundRequested => RefundedAtUtc is not null;

    /// <summary>Fully reverses a settled payment (a dispute resolved wholly for the
    /// customer). Only a Succeeded payment can be refunded, and only once.</summary>
    public void MarkRefunded(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Succeeded payment can be refunded.");
        Status = PaymentStatus.Refunded;
        RefundedAtUtc = now;
        RefundedAmountNaira = AmountNaira;
    }

    /// <summary>Returns PART of a settled payment to the customer (a partial dispute
    /// resolution); the artisan keeps the rest. The payment stays Succeeded (it wasn't
    /// wholly reversed). Amount must be within (0, full) and can only happen once.</summary>
    public void MarkPartiallyRefunded(int amountNaira, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException(
                $"Payment {Id} is {Status}; only a Succeeded payment can be refunded.");
        if (RefundedAtUtc is not null)
            throw new InvalidOperationException($"Payment {Id} was already refunded.");
        if (amountNaira <= 0 || amountNaira >= AmountNaira)
            throw new ArgumentOutOfRangeException(nameof(amountNaira),
                "A partial refund must be greater than 0 and less than the full amount.");
        RefundedAtUtc = now;
        RefundedAmountNaira = amountNaira;
    }

    /// <summary>Records that the provider confirmed the refund reached the customer
    /// (<c>refund.processed</c>). No-op unless a refund was requested (full or partial);
    /// idempotent on repeated webhooks.</summary>
    public void ConfirmRefundSettled(DateTimeOffset now)
    {
        if (RefundedAtUtc is null) return;
        RefundSettledAtUtc ??= now;
        RefundFailedAtUtc = null; // a later success clears an earlier failure
    }

    /// <summary>Records that the provider failed to process the refund
    /// (<c>refund.failed</c>) so it can be retried. No-op unless a refund was requested.</summary>
    public void MarkRefundFailed(DateTimeOffset now)
    {
        if (RefundedAtUtc is null) return;
        if (RefundSettledAtUtc is not null) return; // already landed — ignore
        RefundFailedAtUtc = now;
    }
}
