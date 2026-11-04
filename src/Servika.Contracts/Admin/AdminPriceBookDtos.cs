namespace Servika.Contracts.Admin;

/// <summary>A price-book line in the console, with the signals that say whether its price is right.</summary>
public sealed record AdminPriceBookItemDto(
    Guid Id,
    string CategorySlug,
    string CategoryName,
    string Kind,
    string Name,
    string Scope,
    int? PriceNaira,
    int? RangeMinNaira,
    int? RangeMaxNaira,
    string? PartsNote,
    int? DurationMinutes,
    string? PhotoUrl,
    bool IsPopular,
    bool IsActive,
    int SortOrder,
    bool IsPlaceholderPrice,
    DateTimeOffset UpdatedAtUtc,
    string? UpdatedByName,
    PriceSignalsDto Signals);

/// <summary>
/// How the market is answering a price over the last 30 days. Artisans passing on the job or rotation
/// running out says the price is too low; customers leaving at the payment step or cancelling says it
/// is too high. <c>Verdict</c> is "TooFewJobs", "LooksRight", "MaybeTooLow" or "MaybeTooHigh".
/// </summary>
public sealed record PriceSignalsDto(
    int Booked,
    int Paid,
    int Completed,
    /// <summary>Booked but never paid (customer stopped at the price).</summary>
    int LeftUnpaid,
    int CancelledByCustomer,
    int OffersMade,
    int OffersAccepted,
    int OffersPassed,
    int OffersTimedOut,
    /// <summary>Requests where every eligible artisan was tried and nobody took it.</summary>
    int RanOutOfArtisans,
    /// <summary>The most common reasons artisans gave when passing, most frequent first.</summary>
    IReadOnlyList<string> TopPassReasons,
    string Verdict,
    string VerdictNote);

/// <summary>Create or edit a price-book line (POST/PUT /api/v1/admin/price-book).</summary>
public sealed record SavePriceBookItemRequest(
    string CategorySlug,
    string Kind,
    string Name,
    string Scope,
    int? PriceNaira,
    int? RangeMinNaira,
    int? RangeMaxNaira,
    string? PartsNote,
    int? DurationMinutes,
    bool IsPopular,
    bool IsActive,
    int SortOrder,
    bool IsPlaceholderPrice);

/// <summary>Upload a cover photo for a price-book line (base64 or a data: URI).</summary>
public sealed record SetPriceBookPhotoRequest(string PhotoBase64);

/// <summary>A custom quote waiting for ops (GET /api/v1/admin/quote-reviews).</summary>
public sealed record AdminQuoteReviewDto(
    Guid BidId,
    Guid BookingId,
    string ServiceName,
    string CategorySlug,
    string Description,
    string AddressText,
    string CustomerName,
    string ArtisanName,
    int WorkmanshipNaira,
    int MaterialsNaira,
    int TotalNaira,
    IReadOnlyList<Servika.Contracts.Bookings.BidMaterialLineDto> Materials,
    string? MaterialsNote,
    int? TypicalRangeMinNaira,
    int? TypicalRangeMaxNaira,
    int VisitFeeNaira,
    /// <summary>Why it needs a look (over the threshold, far above the range).</summary>
    IReadOnlyList<string> Reasons,
    DateTimeOffset SubmittedAtUtc);

/// <summary>Ops' answer (POST /api/v1/admin/quote-reviews/{bidId}): "approve", or "sendback" with a note for the artisan.</summary>
public sealed record DecideQuoteReviewRequest(string Outcome, string? Note);

/// <summary>An artisan's service post as ops review it (GET /api/v1/admin/service-posts).</summary>
public sealed record AdminServicePostDto(
    Guid Id,
    string Name,
    int PriceNaira,
    string? PhotoUrl,
    string CategorySlug,
    string CategoryName,
    Guid ArtisanId,
    string ArtisanName,
    string? ArtisanPhotoUrl,
    int? DurationMinutes,
    IReadOnlyList<string> Includes,
    string? Description,
    bool IsActive,
    string Review,
    string? ReviewNote,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

/// <summary>Ops' answer on a post or a gallery photo: "approve", or "reject" with a note for the artisan.</summary>
public sealed record DecidePostRequest(string Outcome, string? Note);

/// <summary>A recent-work photo waiting for ops, image inline.</summary>
public sealed record AdminGalleryPhotoDto(Guid ArtisanId, string ArtisanName, string PhotoKey, string DataUri);
