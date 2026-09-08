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
    bool HasCertificate = false);

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
