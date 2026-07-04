namespace Servika.Domain.Bookings;

/// <summary>
/// A customer's request for an artisan to do a job — the central entity of the
/// marketplace. Like every Domain entity it knows nothing about the database or
/// the web: it only holds the booking's data and the rules that must always be
/// true (its state-machine transitions).
///
/// A booking is born <see cref="BookingStatus.Pending"/> the moment the customer
/// submits it (the <c>Draft</c> stage lives in the mobile app's local draft state,
/// not the database). <c>ServiceName</c> and <c>ArtisanName</c> are denormalised
/// onto the row so listing a customer's history needs no joins — the artisan
/// catalogue is reference data not yet linked to user accounts.
/// </summary>
public sealed class Booking
{
    public Guid Id { get; private set; }

    /// <summary>The customer (a <c>User</c>) who created the booking.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>The pre-selected artisan profile, if the customer chose one when
    /// booking. Null for an open request any matching artisan can accept.</summary>
    public Guid? ArtisanId { get; private set; }

    /// <summary>The service category slug, e.g. "plumbing".</summary>
    public string CategorySlug { get; private set; } = string.Empty;

    /// <summary>Denormalised category name for display, e.g. "Plumbing".</summary>
    public string ServiceName { get; private set; } = string.Empty;

    /// <summary>Denormalised artisan name for display, if one was pre-selected.</summary>
    public string? ArtisanName { get; private set; }

    /// <summary>What the customer needs done.</summary>
    public string Description { get; private set; } = string.Empty;

    public string AddressText { get; private set; } = string.Empty;
    public double? LocationLat { get; private set; }
    public double? LocationLng { get; private set; }

    /// <summary>Access notes — gate code, floor, landmark (optional).</summary>
    public string? LocationInstructions { get; private set; }

    public DateTimeOffset PreferredDate { get; private set; }

    /// <summary>Slot label, e.g. "Morning (8am - 12pm)".</summary>
    public string PreferredTimeSlot { get; private set; } = string.Empty;

    public Urgency Urgency { get; private set; }
    public PricingModel PricingModel { get; private set; }

    /// <summary>Up-front amount in Naira (e.g. the artisan's inspection / call-out
    /// fee) known at booking time. Null when nothing is owed until a quote.</summary>
    public int? InitialQuoteAmountNaira { get; private set; }

    /// <summary>Commission basis recorded at booking time (0 during the launch
    /// window). Stored from day one so monetisation is config, not a redesign.</summary>
    public decimal CommissionRate { get; private set; }

    public BookingStatus Status { get; private set; }

    /// <summary>At-a-glance payment state, advanced by the payments flow.</summary>
    public BookingPaymentState PaymentState { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>When the artisan submitted proof of completed work (→ AwaitingConfirmation).</summary>
    public DateTimeOffset? WorkSubmittedAtUtc { get; private set; }

    /// <summary>Optional note the artisan leaves with their completion proof.</summary>
    public string? CompletionNote { get; private set; }

    /// <summary>Storage keys for the artisan's proof-of-work photos.</summary>
    public List<string> CompletionPhotoKeys { get; private set; } = new();

    /// <summary>When the customer raised a dispute (→ Disputed), if any.</summary>
    public DateTimeOffset? DisputedAtUtc { get; private set; }

    /// <summary>The status the booking held just before it was disputed, so an
    /// admin's resolution can return it to a sensible terminal state.</summary>
    public BookingStatus? PreDisputeStatus { get; private set; }

    // EF Core rebuilds rows through this; private so app code can't skip the rules.
    private Booking() { }

    /// <summary>
    /// Creates and submits a booking. It starts <see cref="BookingStatus.Pending"/>
    /// — the customer has handed the request to the marketplace and is waiting on
    /// an artisan. Display fields (<paramref name="serviceName"/>,
    /// <paramref name="artisanName"/>) are resolved by the caller from the
    /// catalogue and passed in, keeping the Domain free of catalogue lookups.
    /// </summary>
    public static Booking Create(
        Guid customerId,
        Guid? artisanId,
        string categorySlug,
        string serviceName,
        string? artisanName,
        string description,
        string addressText,
        double? locationLat,
        double? locationLng,
        string? locationInstructions,
        DateTimeOffset preferredDate,
        string preferredTimeSlot,
        Urgency urgency,
        PricingModel pricingModel,
        int? initialQuoteAmountNaira,
        decimal commissionRate,
        DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(categorySlug))
            throw new ArgumentException("Service category is required.", nameof(categorySlug));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A job description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(addressText))
            throw new ArgumentException("A service address is required.", nameof(addressText));
        if (commissionRate is < 0 or > 1)
            throw new ArgumentException("Commission rate must be between 0 and 1.", nameof(commissionRate));

        return new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ArtisanId = artisanId,
            CategorySlug = categorySlug.Trim(),
            ServiceName = serviceName.Trim(),
            ArtisanName = string.IsNullOrWhiteSpace(artisanName) ? null : artisanName.Trim(),
            Description = description.Trim(),
            AddressText = addressText.Trim(),
            LocationLat = locationLat,
            LocationLng = locationLng,
            LocationInstructions = string.IsNullOrWhiteSpace(locationInstructions)
                ? null
                : locationInstructions.Trim(),
            // Store as UTC: the client may send a local offset (e.g. +01:00 WAT)
            // but Postgres `timestamptz` only accepts offset-0 instants.
            PreferredDate = preferredDate.ToUniversalTime(),
            PreferredTimeSlot = preferredTimeSlot?.Trim() ?? string.Empty,
            Urgency = urgency,
            PricingModel = pricingModel,
            InitialQuoteAmountNaira = initialQuoteAmountNaira,
            CommissionRate = commissionRate,
            Status = BookingStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Cancels the booking. The customer may only cancel while it is still
    /// <see cref="BookingStatus.Pending"/> or <see cref="BookingStatus.Accepted"/>
    /// — once the artisan is on the way or working, cancellation goes through the
    /// dispute flow (a later slice). Invalid transitions throw so the state
    /// machine can never be driven into an impossible state.
    /// </summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Accepted))
            throw new InvalidBookingStateException(
                $"A booking that is {Status} can no longer be cancelled.");

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = now;
    }

    // ── Artisan-side transitions ──────────────────────────────────────────
    // The assigned artisan drives the job forward through the state machine.
    // Each step guards its own precondition and throws on an illegal jump, so
    // the booking can never be driven into an impossible state (e.g. Arrived
    // before OnMyWay). The API layer is what restricts these to the *assigned*
    // artisan; the Domain only enforces the order of states.

    /// <summary>The artisan accepts a pending request. Pending → Accepted.</summary>
    public void Accept(DateTimeOffset now)
    {
        if (Status is not BookingStatus.Pending)
            throw new InvalidBookingStateException(
                $"Only a Pending booking can be accepted (this one is {Status}).");

        Status = BookingStatus.Accepted;
        AcceptedAtUtc = now;
    }

    /// <summary>The artisan declines a pending request. Pending → Rejected.</summary>
    public void Reject()
    {
        if (Status is not BookingStatus.Pending)
            throw new InvalidBookingStateException(
                $"Only a Pending booking can be rejected (this one is {Status}).");

        Status = BookingStatus.Rejected;
    }

    /// <summary>The artisan starts the trip to the customer. Accepted → OnMyWay.</summary>
    public void StartTrip()
    {
        if (Status is not BookingStatus.Accepted)
            throw new InvalidBookingStateException(
                $"The trip can only start once a booking is Accepted (this one is {Status}).");

        Status = BookingStatus.OnMyWay;
    }

    /// <summary>The artisan reaches the customer. OnMyWay → Arrived.</summary>
    public void Arrive()
    {
        if (Status is not BookingStatus.OnMyWay)
            throw new InvalidBookingStateException(
                $"Arrival can only be marked while OnMyWay (this one is {Status}).");

        Status = BookingStatus.Arrived;
    }

    /// <summary>The artisan begins the work. Arrived → InProgress.</summary>
    public void StartWork()
    {
        if (Status is not BookingStatus.Arrived)
            throw new InvalidBookingStateException(
                $"Work can only start once the artisan has Arrived (this one is {Status}).");

        Status = BookingStatus.InProgress;
    }

    /// <summary>
    /// The artisan submits proof of completed work. InProgress → AwaitingConfirmation.
    /// At least one photo is required (the safeguard that makes auto-confirming an
    /// unresponsive customer fair). The customer then confirms, or it auto-confirms
    /// after the window.
    /// </summary>
    public void SubmitCompletion(IReadOnlyList<string> photoKeys, string? note, DateTimeOffset now)
    {
        if (Status is not BookingStatus.InProgress)
            throw new InvalidBookingStateException(
                $"Work can only be submitted from InProgress (this one is {Status}).");
        if (photoKeys is null || photoKeys.Count == 0)
            throw new InvalidBookingStateException("At least one proof-of-work photo is required.");

        CompletionPhotoKeys = photoKeys.ToList();
        CompletionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        WorkSubmittedAtUtc = now;
        Status = BookingStatus.AwaitingConfirmation;
    }

    /// <summary>
    /// Completes the job. Allowed once the artisan has submitted work
    /// (AwaitingConfirmation) or directly from InProgress (customer/admin closing a
    /// job the artisan didn't formally submit). Used by the customer's confirm and
    /// by the auto-confirm sweep.
    /// </summary>
    public void ConfirmCompletion(DateTimeOffset now)
    {
        if (Status is not (BookingStatus.AwaitingConfirmation or BookingStatus.InProgress))
            throw new InvalidBookingStateException(
                $"A booking can only be completed from InProgress or AwaitingConfirmation (this one is {Status}).");

        Status = BookingStatus.Completed;
        CompletedAtUtc = now;
    }

    // ── Disputes ──────────────────────────────────────────────────────────

    /// <summary>
    /// The customer raises a dispute. Only allowed once work has actually happened
    /// — the job is <see cref="BookingStatus.InProgress"/>,
    /// <see cref="BookingStatus.AwaitingConfirmation"/>, or already
    /// <see cref="BookingStatus.Completed"/> (within the app's dispute window,
    /// enforced by the caller). Records the pre-dispute status so resolution can
    /// restore a terminal state, then freezes the booking in Disputed.
    /// </summary>
    public void RaiseDispute(DateTimeOffset now)
    {
        if (Status is not (BookingStatus.InProgress
            or BookingStatus.AwaitingConfirmation
            or BookingStatus.Completed))
        {
            throw new InvalidBookingStateException(
                $"A {Status} booking can't be disputed — only an in-progress or completed job can.");
        }

        PreDisputeStatus = Status;
        Status = BookingStatus.Disputed;
        DisputedAtUtc = now;
    }

    /// <summary>
    /// Admin closes a dispute: favouring the customer cancels the job (a refund
    /// would follow in a later payments slice); favouring the artisan completes it.
    /// </summary>
    public void ResolveDispute(bool favourCustomer, DateTimeOffset now)
    {
        if (Status is not BookingStatus.Disputed)
            throw new InvalidBookingStateException(
                $"Only a disputed booking can have its dispute resolved (this one is {Status}).");

        if (favourCustomer)
        {
            Status = BookingStatus.Cancelled;
            CancelledAtUtc = now;
        }
        else
        {
            Status = BookingStatus.Completed;
            CompletedAtUtc ??= now;
        }
    }

    /// <summary>Payment has been initialized and is awaiting the gateway result.</summary>
    public void MarkPaymentPending() => PaymentState = BookingPaymentState.Pending;

    /// <summary>Funds received and held in escrow.</summary>
    public void MarkPaid() => PaymentState = BookingPaymentState.Paid;

    /// <summary>Escrow returned to the customer (e.g. a dispute resolved in their favour).</summary>
    public void MarkRefunded() => PaymentState = BookingPaymentState.Refunded;
}
