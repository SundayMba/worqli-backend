namespace Servika.Contracts.Catalogue;

/// <summary>
/// The signed-in artisan's own profile (GET /api/v1/artisan/profile). Includes the
/// <see cref="VerificationStatus"/> the public catalogue DTOs omit, so the Pro app
/// can show onboarding/verification state.
/// </summary>
public sealed record MyArtisanProfileDto(
    Guid Id,
    string ImageKey,
    string FullName,
    string Specialty,
    double Rating,
    int ReviewCount,
    bool IsAvailable,
    string VerificationStatus,
    int ExperienceYears,
    string Location,
    int InspectionFeeNaira,
    string About,
    IReadOnlyList<string> CategorySlugs,
    IReadOnlyList<string> Services,
    /// <summary>API path of the artisan's uploaded photo, or null.</summary>
    string? PhotoUrl,
    /// <summary>API path of the artisan's uploaded cover photo, or null.</summary>
    string? CoverPhotoUrl,
    /// <summary>API paths of the uploaded work-gallery photos, newest first.</summary>
    IReadOnlyList<string> GalleryUrls,
    /// <summary>True when a work certificate was uploaded.</summary>
    bool HasCertificate);

/// <summary>
/// Create or update the signed-in artisan's profile (POST/PUT /api/v1/artisan/profile),
/// mirroring the Pro onboarding + service-setup screens.
/// </summary>
public sealed record SaveArtisanProfileRequest(
    string Specialty,
    List<string> CategorySlugs,
    List<string> Services,
    string About,
    int ExperienceYears,
    string Location,
    int InspectionFeeNaira,
    double? Latitude,
    double? Longitude,
    string? ImageKey,
    /// <summary>Optional profile photo (raw base64 or data: URI). When present
    /// it's stored and becomes the artisan's public avatar.</summary>
    string? PhotoBase64 = null,
    /// <summary>Optional cover photo — a shot of the artisan at work — shown at
    /// the top of their public profile.</summary>
    string? CoverPhotoBase64 = null,
    /// <summary>Optional work certificate (image). Purely a trust signal —
    /// artisans without one onboard exactly the same.</summary>
    string? CertificateBase64 = null);

/// <summary>POST /api/v1/artisan/gallery body — one work-evidence photo.</summary>
public sealed record AddGalleryPhotoRequest(string PhotoBase64);

/// <summary>The artisan's own gallery after an add/remove.</summary>
public sealed record GalleryDto(IReadOnlyList<string> GalleryUrls);
