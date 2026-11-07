namespace Servika.Contracts.Bookings;

/// <summary>
/// The full booking record for the detail screen (GET /api/v1/bookings/{id} and
/// the create response). <c>Status</c>, <c>Urgency</c> and <c>PricingModel</c>
/// are readable strings.
/// </summary>
public sealed record BookingDetailDto(
    Guid Id,
    string Status,
    Guid CustomerId,
    Guid? ArtisanId,
    string CategorySlug,
    string ServiceName,
    string? ArtisanName,
    /// <summary>The customer's display name — filled on the ARTISAN-side job
    /// detail (drives the map's name tag); null on customer-side responses,
    /// where the caller is the customer.</summary>
    string? CustomerName,
    string Description,
    string AddressText,
    double? LocationLat,
    double? LocationLng,
    string? LocationInstructions,
    DateTimeOffset PreferredDate,
    string PreferredTimeSlot,
    string Urgency,
    string PricingModel,
    string PaymentState,
    /// <summary>"Online" (escrow, default) or "Cash" (pay after service).</summary>
    string PaymentMethod,
    int? InitialQuoteAmountNaira,
    /// <summary>Amount returned to the customer if refunded (full or partial), else null.</summary>
    int? RefundedAmountNaira,
    decimal CommissionRate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    /// <summary>When the artisan started the work (drives the job timer).</summary>
    DateTimeOffset? WorkStartedAtUtc,
    /// <summary>"Inspection" or "RemoteQuote" (bidding).</summary>
    string AssessmentMode,
    /// <summary>API paths of the customer's job photos.</summary>
    IReadOnlyList<string> MediaUrls,
    /// <summary>API path of the customer's short job video, or null.</summary>
    string? VideoUrl,
    /// <summary>Active bids on this request (RemoteQuote + Open only; 0 otherwise).</summary>
    int BidCount,
    /// <summary>Labour / materials split of the agreed price, when the accepted
    /// quote was itemised; null otherwise.</summary>
    int? AgreedWorkmanshipNaira,
    int? AgreedMaterialsNaira,
    /// <summary>"None" | "Requested" | "Approved" | "Declined": the artisan's request to
    /// have part of the materials money released from escrow before completion.</summary>
    string MaterialsAdvanceStatus,
    int? MaterialsAdvanceNaira,
    /// <summary>How many jobs this customer has completed on Servika — the
    /// artisan-side "7 jobs booked" trust line. Null on customer-side responses.</summary>
    int? CustomerCompletedJobs,
    /// <summary>ARTISAN-facing only: what the artisan receives for this job (agreed
    /// amount − commission − released materials advance). Null customer-side.</summary>
    int? ArtisanNetNaira,
    /// <summary>When an AwaitingConfirmation job confirms itself (work submitted +
    /// the admin auto-confirm window); null in every other status.</summary>
    DateTimeOffset? AutoConfirmAtUtc,
    /// <summary>How many artisans Servika has offered this job to (Open requests).</summary>
    int OfferRound = 0,
    /// <summary>Set while rotation has nobody left to ask; Servika keeps retrying.</summary>
    DateTimeOffset? OffersExhaustedAtUtc = null,
    /// <summary>Rotation | Direct | Code | Rebook.</summary>
    string Source = "Direct",
    /// <summary>The Servika price-book job this booking is for, if any (paid upfront at Servika's price).</summary>
    Guid? PriceBookItemId = null,
    /// <summary>True while a price-book job waits for its upfront payment; no artisan is offered it yet.</summary>
    bool AwaitingUpfrontPayment = false,
    /// <summary>Diagnosis visit: the fee paid at booking, credited against the repairs. Null otherwise.</summary>
    int? DiagnosisFeeNaira = null,
    /// <summary>Repairs from Servika's list the customer approved on this job.</summary>
    int ApprovedRepairsNaira = 0,
    /// <summary>Held in escrow so far, and what is still owed against the agreed price.</summary>
    int PaidNaira = 0,
    int AmountDueNaira = 0,
    /// <summary>A custom job: a paid visit, then an itemised quote checked against the typical range.</summary>
    bool IsCustomQuote = false,
    int? TypicalRangeMinNaira = null,
    int? TypicalRangeMaxNaira = null,
    /// <summary>The 4-digit code the customer reads out at the door. Customer-side only, and only until
    /// the artisan enters it; never sent to the artisan.</summary>
    string? DoorCode = null,
    /// <summary>The artisan has arrived and must still enter the door code.</summary>
    bool DoorCodeRequired = false,
    DateTimeOffset? DoorCodeConfirmedAtUtc = null,
    /// <summary>Too many wrong codes: the customer must show a new one.</summary>
    bool DoorCodeLocked = false,
    int DoorCodeAttemptsLeft = 0);

/// <summary>POST /api/v1/artisan/jobs/{id}/door-code body: the code the customer read out.</summary>
public sealed record ConfirmDoorCodeRequest(string? Code);
