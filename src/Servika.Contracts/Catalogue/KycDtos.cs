namespace Servika.Contracts.Catalogue;

/// <summary>
/// Submit KYC for the signed-in artisan (POST /api/v1/artisan/kyc). Images are
/// base64 (optionally a data: URI) — a selfie + one government ID. <see cref="IdType"/>
/// is "Nin" / "VotersCard" / "DriversLicense" / "Passport".
/// </summary>
public sealed record SubmitKycRequest(
    string IdType,
    string? IdNumber,
    string SelfieBase64,
    string IdImageBase64);

/// <summary>
/// The artisan's KYC status (GET /api/v1/artisan/kyc). <see cref="Status"/> is
/// "NotSubmitted" / "Pending" / "Verified" / "Rejected".
/// </summary>
public sealed record KycStatusDto(
    string Status,
    string? IdType,
    string? IdNumber,
    string? ReviewNote,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);
