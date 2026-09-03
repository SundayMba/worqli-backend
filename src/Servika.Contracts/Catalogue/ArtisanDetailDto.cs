namespace Servika.Contracts.Catalogue;

/// <summary>
/// The full artisan profile (GET /api/v1/artisans/{id}). Extends the summary
/// fields with the details the profile screen needs. The client resolves the
/// avatar/cover from <see cref="ImageKey"/> and the gallery from
/// <see cref="GalleryKeys"/>; <see cref="InspectionFeeNaira"/> is formatted on
/// the client (e.g. "₦5,000").
/// </summary>
public sealed record ArtisanDetailDto(
    Guid Id,
    string ImageKey,
    string FullName,
    string Specialty,
    double Rating,
    int ReviewCount,
    double DistanceKm,
    bool IsAvailable,
    string Accent,
    int ExperienceYears,
    string Location,
    string ResponseTime,
    string JobsCount,
    int InspectionFeeNaira,
    string About,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> GalleryKeys,
    /// <summary>Category slugs this artisan serves; the first is treated as
    /// primary when pre-filling a booking's service category.</summary>
    IReadOnlyList<string> CategorySlugs,
    /// <summary>API path of the artisan's uploaded profile photo, or null
    /// (client falls back to the bundled <see cref="ImageKey"/> art).</summary>
    string? PhotoUrl,
    /// <summary>API path of the artisan's uploaded cover photo (them at work),
    /// or null — the client then covers with the profile photo / bundled art.</summary>
    string? CoverPhotoUrl,
    /// <summary>API paths of the artisan's uploaded work-evidence photos,
    /// newest first. Empty → the client falls back to bundled gallery art.</summary>
    IReadOnlyList<string> GalleryUrls,
    /// <summary>True when the artisan uploaded a work certificate.</summary>
    bool HasCertificate,
    /// <summary>The artisan's published fixed-price services ("Knotless braids —
    /// ₦15,000") — bookable directly at that price, paid once the artisan
    /// accepts. Empty for quote-only artisans.</summary>
    IReadOnlyList<ArtisanServiceDto>? PricedServices = null);

/// <summary>A fixed-price service on an artisan's profile. <see cref="PhotoUrl"/>
/// is the API path of its showcase photo, or null (clients fall back to the
/// artisan's photo).</summary>
public sealed record ArtisanServiceDto(Guid Id, string Name, int PriceNaira, string? PhotoUrl = null);

/// <summary>POST /api/v1/artisan/services body. Re-posting a name revises its price
/// (and replaces the photo when one is sent; omitted = keep the current photo).</summary>
public sealed record SaveArtisanServiceRequest(string Name, int PriceNaira, string? PhotoBase64 = null);

/// <summary>One card on the Home fixed-price discovery rail (GET /api/v1/services/featured):
/// a bookable service plus enough of its artisan's reputation to book on the spot.</summary>
public sealed record FeaturedServiceDto(
    Guid ServiceId,
    string Name,
    int PriceNaira,
    /// <summary>The service's own showcase photo, or null → clients fall back to
    /// <see cref="ArtisanPhotoUrl"/>, then initials.</summary>
    string? PhotoUrl,
    Guid ArtisanId,
    string ArtisanName,
    double Rating,
    int ReviewCount,
    bool HasCertificate,
    string? ArtisanPhotoUrl,
    bool IsAvailable,
    /// <summary>Km from the caller's coords when provided; null otherwise.</summary>
    double? DistanceKm,
    /// <summary>The artisan's primary category slug — drives the client's
    /// fallback artwork when the service has no photo.</summary>
    string? CategorySlug = null);
