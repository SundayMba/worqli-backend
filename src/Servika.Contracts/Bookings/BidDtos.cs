namespace Servika.Contracts.Bookings;

/// <summary>
/// An artisan's price offer on an open RemoteQuote request, as the customer
/// reviews it — carries enough of the artisan's reputation (rating, reviews,
/// certificate, photo) to choose without leaving the list.
/// </summary>
public sealed record BidDto(
    Guid Id,
    Guid BookingId,
    Guid ArtisanId,
    string ArtisanName,
    double Rating,
    int ReviewCount,
    bool HasCertificate,
    string? PhotoUrl,
    int AmountNaira,
    string? MaterialsNote,
    string Status,
    DateTimeOffset CreatedAtUtc,
    /// <summary>Km from the job to the artisan's base location, when both are
    /// known — lets the customer sort offers by proximity. Null otherwise.</summary>
    double? DistanceKm = null,
    /// <summary>Labour part of the quote; <c>AmountNaira</c> = this + materials.</summary>
    int WorkmanshipNaira = 0,
    /// <summary>Sum of the itemised material lines.</summary>
    int MaterialsNaira = 0,
    /// <summary>Itemised materials/parts, empty for a labour-only quote.</summary>
    IReadOnlyList<BidMaterialLineDto>? Materials = null,
    /// <summary>The customer's pending counter-offer on workmanship, if any.</summary>
    int? PendingCounterNaira = null,
    string? PendingCounterNote = null,
    /// <summary>Counter-offers made so far / the cap.</summary>
    int CounterRounds = 0,
    int MaxCounterRounds = 3);

/// <summary>POST /api/v1/bookings/{id}/bids/{bidId}/counter body: the customer's
/// proposed WORKMANSHIP price (materials stay as itemised).</summary>
public sealed record CounterBidRequest(int WorkmanshipNaira, string? Note);

/// <summary>One material/parts line on an itemised quote.</summary>
public sealed record BidMaterialLineDto(string Name, int Quantity, int UnitPriceNaira);

/// <summary>POST /api/v1/artisan/jobs/{id}/bid body. Re-posting revises the bid.
/// Itemised: send <c>WorkmanshipNaira</c> + <c>Materials</c>; the total is computed
/// server-side. Legacy single-price clients send only <c>AmountNaira</c>, which is
/// then treated as all-workmanship.</summary>
public sealed record SubmitBidRequest(
    int AmountNaira,
    string? MaterialsNote,
    int? WorkmanshipNaira = null,
    IReadOnlyList<BidMaterialLineDto>? Materials = null);
