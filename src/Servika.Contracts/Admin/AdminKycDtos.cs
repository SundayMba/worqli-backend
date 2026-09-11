namespace Servika.Contracts.Admin;

/// <summary>A KYC submission in the admin verification queue (list view).</summary>
public sealed record KycSubmissionDto(
    Guid Id,
    Guid UserId,
    string ArtisanName,
    string Email,
    string IdType,
    string? IdNumber,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNote);

/// <summary>
/// A KYC submission with the document images inlined as base64 data URIs, for the
/// admin to eyeball the selfie against the ID before deciding.
/// </summary>
public sealed record KycSubmissionDetailDto(
    Guid Id,
    Guid UserId,
    string ArtisanName,
    string Email,
    string IdType,
    string? IdNumber,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNote,
    string? SelfieDataUri,
    string? IdImageDataUri,
    /// <summary>The artisan's guarantors (people Servika may call), with ID photos inlined.</summary>
    IReadOnlyList<AdminGuarantorDto>? Guarantors = null,
    /// <summary>Saved payout account summary — the name should match the ID.</summary>
    string? PayoutBankName = null,
    string? PayoutAccountMasked = null,
    string? PayoutAccountName = null,
    /// <summary>Trade / area / photo context for the reviewer.</summary>
    string? Specialty = null,
    string? Location = null,
    string? ProfilePhotoUrl = null,
    bool HasCertificate = false,
    /// <summary>The artisan profile id (for the per-artisan actions such as the guarantor waiver).</summary>
    Guid? ArtisanId = null,
    bool GuarantorsRequired = true,
    bool GuarantorsWaived = false,
    int RequiredGuarantorCount = 2,
    /// <summary>What the NIN register said when the artisan checked their number: Matched | NameMismatch | NotFound | Failed | null.</summary>
    string? NinLookupStatus = null,
    string? NinLookupName = null,
    DateTimeOffset? NinLookupCheckedAtUtc = null);

/// <summary>A guarantor as the admin sees it during review.</summary>
public sealed record AdminGuarantorDto(
    Guid Id,
    string FullName,
    string Phone,
    string Relationship,
    int YearsKnown,
    string? Occupation,
    string? Address,
    string? IdPhotoDataUri);

/// <summary>Reject a KYC submission with a reason (POST /admin/kyc/{id}/reject).</summary>
public sealed record RejectKycRequest(string? Reason);

/// <summary>Turn the guarantor requirement off (or back on) for one artisan (POST /admin/artisans/{id}/guarantor-waiver).</summary>
public sealed record GuarantorWaiverRequest(bool Waived);
