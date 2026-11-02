namespace Servika.Contracts.Catalogue;

/// <summary>
/// The little an artisan's booking code reveals (GET /api/v1/artisans/by-code/{code}, anonymous):
/// enough for "Book Emeka", nothing to browse by.
/// </summary>
public sealed record ArtisanByCodeDto(
    Guid Id,
    string FirstName,
    string FullName,
    string Specialty,
    string? PhotoUrl,
    string Location,
    bool IsAvailable,
    bool IsAway,
    int CompletedJobs,
    IReadOnlyList<string> CategorySlugs,
    string BookingCode);

/// <summary>An artisan this customer has used before (GET /api/v1/bookings/past-artisans).</summary>
public sealed record PastArtisanDto(
    Guid ArtisanId,
    string FullName,
    string Specialty,
    string ImageKey,
    string? PhotoUrl,
    IReadOnlyList<string> CategorySlugs,
    /// <summary>Completed jobs with this customer.</summary>
    int JobsWithYou,
    DateTimeOffset LastBookedAtUtc,
    bool IsAvailable,
    bool IsAway,
    /// <summary>False once the artisan left or lost verification; the app then hides Book again.</summary>
    bool CanBook);
