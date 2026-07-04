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
    string? IdImageDataUri);

/// <summary>Reject a KYC submission with a reason (POST /admin/kyc/{id}/reject).</summary>
public sealed record RejectKycRequest(string? Reason);
