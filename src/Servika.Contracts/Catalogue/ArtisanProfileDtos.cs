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
    IReadOnlyList<string> Services);

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
    string? ImageKey);
