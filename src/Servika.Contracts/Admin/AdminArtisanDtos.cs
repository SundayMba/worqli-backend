namespace Servika.Contracts.Admin;

/// <summary>
/// An artisan in the admin directory — identity, reputation, verification status,
/// and (from commission-on-cash) their standing: how much unpaid cash-job service
/// fee they owe and whether that debt has restricted them from new jobs.
/// </summary>
public sealed record AdminArtisanDto(
    Guid Id,
    Guid? UserId,
    string FullName,
    string Email,
    string Specialty,
    double Rating,
    int ReviewCount,
    bool IsAvailable,
    string VerificationStatus,
    bool HasCertificate,
    IReadOnlyList<string> CategorySlugs,
    /// <summary>Relative API path of the uploaded profile photo (public), or null.</summary>
    string? PhotoUrl,
    int GalleryCount,
    /// <summary>Unpaid cash-job service fees not covered by earnings (0 = clear).</summary>
    int CommissionOwedNaira,
    /// <summary>True when the debt is past the platform limit — no new job requests.</summary>
    bool IsRestricted);

/// <summary>An artisan's uploaded work certificate, inlined as a base64 data URI
/// (like the KYC document images) so the admin can view it in the browser.</summary>
public sealed record AdminArtisanCertificateDto(string? CertificateDataUri);
