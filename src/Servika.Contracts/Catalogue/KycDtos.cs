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

/// <summary>POST /api/v1/artisan/kyc/nin/verify body.</summary>
public sealed record VerifyNinRequest(string Nin);

/// <summary>
/// Outcome of a NIN register check. Status: Matched (register name matches the account
/// name), NameMismatch, NotFound, Failed (provider error), Unavailable (no provider
/// configured, the reviewer will check by hand). The name is masked to initials.
/// </summary>
public sealed record NinVerifyResultDto(string Status, string? RegisterName, int LookupsLeft);
