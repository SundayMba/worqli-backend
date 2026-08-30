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

    /// <summary>The agreed job price in Naira, set when the customer accepts an
    /// artisan's price offer (<see cref="AcceptBid"/>). Null until then — booking
    /// is always free; nothing is owed before a price has been agreed.</summary>
    public int? InitialQuoteAmountNaira { get; private set; }

    /// <summary>How the agreed price splits: labour vs itemised materials. Set when a
    /// quote is accepted (null for fixed-price listings and legacy quotes). The
    /// materials part is the ceiling for a materials advance.</summary>
    public int? AgreedWorkmanshipNaira { get; private set; }
    public int? AgreedMaterialsNaira { get; private set; }

    /// <summary>Materials advance: the artisan may ask for up to the agreed materials
    /// amount to be released from the paid escrow BEFORE completion, and the customer
    /// approves or declines. One advance per booking; a decline can be re-asked.</summary>
    public MaterialsAdvanceStatus MaterialsAdvanceStatus { get; private set; } = MaterialsAdvanceStatus.None;
    public int? MaterialsAdvanceNaira { get; private set; }
    public DateTimeOffset? MaterialsAdvanceRequestedAtUtc { get; private set; }
    public DateTimeOffset? MaterialsAdvanceDecidedAtUtc { get; private set; }

    /// <summary>The advance actually released to the artisan (0 unless Approved).</summary>
    public int ReleasedMaterialsAdvanceNaira =>
        MaterialsAdvanceStatus == MaterialsAdvanceStatus.Approved ? MaterialsAdvanceNaira ?? 0 : 0;

    /// <summary>The artisan asks for materials money up front. Only on a booking that
    /// is paid into escrow (cash jobs: the customer hands over cash directly), with an
    /// itemised materials amount agreed, while the job is live, and not already
    /// approved. Capped at the agreed materials total.</summary>
    public void RequestMaterialsAdvance(int amountNaira, DateTimeOffset now)
    {
        if (Status is not (BookingStatus.Accepted or BookingStatus.OnMyWay
            or BookingStatus.Arrived or BookingStatus.InProgress))
            throw new InvalidBookingStateException(
                $"A materials advance can't be requested while the job is {Status}.");
        if (PaymentState != BookingPaymentState.Paid)
            throw new InvalidBookingStateException(
                "The customer hasn't paid into escrow yet, so there is nothing to release. " +
                "On a cash job, ask the customer for the materials money directly.");
        if (AgreedMaterialsNaira is not { } materials || materials <= 0)
            throw new InvalidBookingStateException(
                "This quote has no itemised materials. Only the materials part of a price can be advanced.");
        if (MaterialsAdvanceStatus == MaterialsAdvanceStatus.Approved)
            throw new InvalidBookingStateException("A materials advance was already released for this job.");
        if (amountNaira <= 0 || amountNaira > materials)
            throw new ArgumentException(
                $"Ask for between ₦1 and the agreed materials total (₦{materials:N0}).", nameof(amountNaira));

        MaterialsAdvanceStatus = MaterialsAdvanceStatus.Requested;
        MaterialsAdvanceNaira = amountNaira;
        MaterialsAdvanceRequestedAtUtc = now;
        MaterialsAdvanceDecidedAtUtc = null;
    }

    /// <summary>The customer releases the requested advance. Guards that escrow is
    /// still held (a refund in between would have emptied it).</summary>
    public void ApproveMaterialsAdvance(DateTimeOffset now)
    {
        if (MaterialsAdvanceStatus != MaterialsAdvanceStatus.Requested)
            throw new InvalidBookingStateException("There is no pending materials request to approve.");
        if (PaymentState != BookingPaymentState.Paid)
            throw new InvalidBookingStateException("The escrow for this booking is no longer held.");
        MaterialsAdvanceStatus = MaterialsAdvanceStatus.Approved;
        MaterialsAdvanceDecidedAtUtc = now;
    }

    public void DeclineMaterialsAdvance(DateTimeOffset now)
    {
        if (MaterialsAdvanceStatus != MaterialsAdvanceStatus.Requested)
            throw new InvalidBookingStateException("There is no pending materials request to decline.");
        MaterialsAdvanceStatus = MaterialsAdvanceStatus.Declined;
        MaterialsAdvanceDecidedAtUtc = now;
    }

    /// <summary>Commission basis recorded at booking time (0 during the launch
    /// window). Stored from day one so monetisation is config, not a redesign.</summary>
    public decimal CommissionRate { get; private set; }

    public BookingStatus Status { get; private set; }

    /// <summary>At-a-glance payment state, advanced by the payments flow.</summary>
    public BookingPaymentState PaymentState { get; private set; }

    /// <summary>How the agreed price gets settled — online escrow (default) or
    /// cash after service. The customer chooses at the payment moment; work
    /// can't start until the price is secured one way or the other.</summary>
    public PaymentMethod PaymentMethod { get; private set; } = PaymentMethod.Online;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>When the artisan actually started the work (→ InProgress) —
    /// drives the live elapsed-time display on the job screen.</summary>
    public DateTimeOffset? WorkStartedAtUtc { get; private set; }

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

    /// <summary>How the price gets determined (customer's choice at request
    /// time): the artisan inspects in person, or the job is priced remotely
    /// from the customer's photos/video via bidding.</summary>
    public AssessmentMode Assessment { get; private set; } = AssessmentMode.Inspection;

    /// <summary>Storage keys of the customer's job photos (context for the
    /// artisan; required context for RemoteQuote bidding).</summary>
    public List<string> MediaKeys { get; private set; } = new();

    /// <summary>Storage key of the customer's short job video clip, if any.</summary>
    public string? VideoKey { get; private set; }

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
        DateTimeOffset now,
        AssessmentMode assessment = AssessmentMode.Inspection,
        List<string>? mediaKeys = null,
        string? videoKey = null)
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
            // With a chosen artisan the request goes straight to them (Pending);
            // with none it's an Open request any matching artisan can claim.
            Status = artisanId is null ? BookingStatus.Open : BookingStatus.Pending,
            CreatedAt = now,
            // Remote pricing only makes sense on an open request that artisans
            // will assess from media; a direct booking keeps the normal flow.
            Assessment = artisanId is null ? assessment : AssessmentMode.Inspection,
            MediaKeys = mediaKeys ?? new(),
            VideoKey = string.IsNullOrWhiteSpace(videoKey) ? null : videoKey,
        };
    }

    /// <summary>
    /// The customer accepts an artisan's price offer. Two paths land here:
    /// an <b>open</b> RemoteQuote request accepting any bidder (the booking is
    /// assigned to that artisan), or a <b>direct</b> Pending request accepting
    /// the pre-selected artisan's quote. Either way the offer becomes the
    /// booking's price and it moves to Accepted — payment happens against this
    /// agreed amount, never before ("pay only when a price has been agreed").
    /// </summary>
    public void AcceptBid(
        Guid artisanId, string artisanName, int amountNaira, DateTimeOffset now,
        int? workmanshipNaira = null, int? materialsNaira = null)
    {
        AcceptBidCore(artisanId, artisanName, amountNaira, now);
        AgreedWorkmanshipNaira = workmanshipNaira;
        AgreedMaterialsNaira = materialsNaira;
    }

    private void AcceptBidCore(Guid artisanId, string artisanName, int amountNaira, DateTimeOffset now)
    {
        // Open broadcast: any bidder can win — acceptance assigns the artisan.
        if (Status is BookingStatus.Open)
        {
            if (Assessment is not AssessmentMode.RemoteQuote)
                throw new InvalidBookingStateException(
                    "This request is inspect-first — artisans accept it directly instead of bidding.");

            ArtisanId = artisanId;
            ArtisanName = artisanName;
            InitialQuoteAmountNaira = amountNaira;
            Status = BookingStatus.Accepted;
            AcceptedAtUtc = now;
            return;
        }

        // Direct booking: only the pre-selected artisan's quote can be accepted.
        if (ArtisanId is null || ArtisanId != artisanId)
            throw new InvalidBookingStateException(
                "Only the requested artisan's quote can be accepted on this booking.");

        // Pre-visit quote: accepting doubles as accepting the job (→ Accepted).
        if (Status is BookingStatus.Pending)
        {
            InitialQuoteAmountNaira = amountNaira;
            Status = BookingStatus.Accepted;
            AcceptedAtUtc = now;
            return;
        }

        // On-site / en-route quote: the artisan already accepted-to-inspect, so
        // acceptance only fixes the price — the trip status stays where it is.
        if (Status is BookingStatus.Accepted or BookingStatus.OnMyWay or BookingStatus.Arrived)
        {
            if (PaymentState == BookingPaymentState.Paid)
                throw new InvalidBookingStateException(
                    "The price is already agreed and paid — it can't change now.");

            InitialQuoteAmountNaira = amountNaira;
            return;
        }

        throw new InvalidBookingStateException(
            $"A price offer can't be accepted while the booking is {Status}.");
    }

    // NOTE: claiming an open request (Open → Accepted, assigning the artisan) is done
    // as a single atomic guarded UPDATE in the repository (IBookingRepository.
    // TryClaimAsync) rather than a load-mutate-save here. That's deliberate: it's the
    // one transition many artisans can attempt at once, so the single-winner guarantee
    // has to live in the write itself (WHERE Status = Open) — an in-memory guard can't
    // prevent two callers who both loaded it Open from both saving.

    /// <summary>
    /// Cancels the booking. Either party may cancel any time up to and including
    /// <see cref="BookingStatus.Arrived"/> — plans change, vehicles break down.
    /// Once work is <see cref="BookingStatus.InProgress"/> cancellation is no
    /// longer allowed; that's what the dispute flow is for (otherwise a customer
    /// could cancel mid-repair and skip paying). Escrow already paid is refunded
    /// by the calling handler. Invalid transitions throw so the state machine can
    /// never be driven into an impossible state.
    /// </summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (BookingStatus.Open or BookingStatus.Pending or BookingStatus.Accepted
            or BookingStatus.OnMyWay or BookingStatus.Arrived))
        {
            throw new InvalidBookingStateException(
                $"A booking that is {Status} can no longer be cancelled.");
        }

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

    /// <summary>
    /// The customer re-broadcasts a direct request to every matching artisan —
    /// the escape hatch when their chosen artisan declined (Rejected) or never
    /// responded (still Pending). The artisan is unassigned and the booking
    /// becomes an Open request, keeping its description, media and schedule so
    /// nothing is re-typed. With photos/video attached it reopens in bidding
    /// mode (artisans send prices); bare requests reopen first-to-claim.
    /// </summary>
    public void Rebroadcast()
    {
        if (ArtisanId is null)
            throw new InvalidBookingStateException("This request is already open to all artisans.");
        if (Status is not (BookingStatus.Pending or BookingStatus.Rejected))
            throw new InvalidBookingStateException(
                $"A booking that is {Status} can't be re-broadcast — only an unanswered or declined request can.");

        ArtisanId = null;
        ArtisanName = null;
        Status = BookingStatus.Open;
        Assessment = MediaKeys.Count > 0 || VideoKey is not null
            ? AssessmentMode.RemoteQuote
            : AssessmentMode.Inspection;
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

    /// <summary>
    /// The artisan begins the work. Arrived → InProgress — but only once the
    /// agreed price is secured: paid into escrow, or the customer explicitly
    /// chose cash. This is the pay-before-work gate that makes escrow the spine
    /// of the marketplace instead of an optional step.
    /// </summary>
    public void StartWork(DateTimeOffset now)
    {
        if (Status is not BookingStatus.Arrived)
            throw new InvalidBookingStateException(
                $"Work can only start once the artisan has Arrived (this one is {Status}).");
        if (InitialQuoteAmountNaira is null or <= 0)
            throw new InvalidBookingStateException(
                "A price must be agreed before work starts — send the customer your quote first.");
        if (PaymentState != BookingPaymentState.Paid && PaymentMethod != PaymentMethod.Cash)
            throw new InvalidBookingStateException(
                "Waiting for the customer's payment — work can start once it's secured in escrow (or they choose cash).");

        Status = BookingStatus.InProgress;
        WorkStartedAtUtc = now;
    }

    /// <summary>
    /// The customer picks how they'll settle the agreed price: online escrow
    /// (default) or cash after service. Only before the work starts, and never
    /// after an online payment has already been made.
    /// </summary>
    public void ChoosePaymentMethod(PaymentMethod method)
    {
        if (Status is not (BookingStatus.Open or BookingStatus.Pending or BookingStatus.Accepted
            or BookingStatus.OnMyWay or BookingStatus.Arrived))
        {
            throw new InvalidBookingStateException(
                $"The payment method can't change once the job is {Status}.");
        }
        if (PaymentState is BookingPaymentState.Paid or BookingPaymentState.Refunded)
            throw new InvalidBookingStateException(
                "This booking has already been paid — the payment method is settled.");

        PaymentMethod = method;
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

    /// <summary>Funds received and held in escrow. An escrow settlement is
    /// online by definition, so a stale cash choice is overwritten.</summary>
    public void MarkPaid()
    {
        PaymentState = BookingPaymentState.Paid;
        PaymentMethod = PaymentMethod.Online;
    }

    /// <summary>Escrow returned to the customer (e.g. a dispute resolved in their favour).</summary>
    /// <summary>How much was returned to the customer (full or partial refund),
    /// for display. Null until a refund happens.</summary>
    public int? RefundedAmountNaira { get; private set; }

    public void MarkRefunded(int amountNaira)
    {
        PaymentState = BookingPaymentState.Refunded;
        RefundedAmountNaira = amountNaira;
    }

    public void MarkPartiallyRefunded(int amountNaira)
    {
        PaymentState = BookingPaymentState.PartiallyRefunded;
        RefundedAmountNaira = amountNaira;
    }

    /// <summary>Resolves a dispute with a PARTIAL refund: the customer gets some
    /// money back and the artisan keeps the rest for the work they did, so the job
    /// is Completed rather than Cancelled.</summary>
    public void ResolveDisputePartial(DateTimeOffset now)
    {
        if (Status is not BookingStatus.Disputed)
            throw new InvalidBookingStateException(
                $"Only a disputed booking can have its dispute resolved (this one is {Status}).");
        Status = BookingStatus.Completed;
        CompletedAtUtc ??= now;
    }
}
