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
    double? DistanceKm = null);

/// <summary>POST /api/v1/artisan/jobs/{id}/bid body. Re-posting revises the bid.</summary>
public sealed record SubmitBidRequest(
    int AmountNaira,
    string? MaterialsNote);
