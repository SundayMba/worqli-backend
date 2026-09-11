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
    string IdImageBase64,
    /// <summary>Extra live selfies in poses the app asked for ("left", "right"), so the reviewer can tell a live face from a photo of a photo.</summary>
    IReadOnlyList<PoseSelfieRequest>? PoseSelfies = null);

public sealed record PoseSelfieRequest(string Pose, string ImageBase64);

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
    DateTimeOffset? ReviewedAtUtc,
    /// <summary>The check the reviewer asked to be fixed (Trade | Photo | Identity | Selfie | Guarantors | Payout | Documents), or null.</summary>
    string? OpenCheck = null,
    string? OpenReasonCode = null,
    string? OpenNote = null,
    int ResubmissionCount = 0,
    /// <summary>The application's history, newest first. Reviewer identities are not exposed to the artisan.</summary>
    IReadOnlyList<VerificationEventDto>? Events = null);

/// <summary>One line of an application's history.</summary>
public sealed record VerificationEventDto(
    Guid Id,
    /// <summary>Submitted | Resubmitted | ChangesRequested | Approved | Declined.</summary>
    string Action,
    string? Check,
    string? ReasonCode,
    string? Note,
    /// <summary>"You" for the artisan's own actions; the reviewer's name on the admin side, "Reviewer" on the artisan side.</summary>
    string Actor,
    DateTimeOffset CreatedAtUtc);

/// <summary>Reason codes the review desk uses. Both apps map them to labels; the note carries the specifics.</summary>
public static class VerificationReasonCodes
{
    public const string DocumentUnreadable = "document_unreadable";
    public const string NameMismatch = "name_mismatch";
    public const string PhotoNotAFace = "photo_not_a_face";
    public const string SelfieDoesNotMatch = "selfie_does_not_match";
    public const string NumberDoesNotMatchDocument = "number_does_not_match_document";
    public const string GuarantorUnreachable = "guarantor_unreachable";
    public const string GuarantorIdMissing = "guarantor_id_missing";
    public const string PayoutNameDiffers = "payout_name_differs";
    public const string TradeUnclear = "trade_unclear";
    public const string Other = "other";
}

/// <summary>POST /api/v1/artisan/kyc/nin/verify body.</summary>
public sealed record VerifyNinRequest(string Nin);

/// <summary>
/// Outcome of a NIN register check. Status: Matched (register name matches the account
/// name), NameMismatch, NotFound, Failed (provider error), Unavailable (no provider
/// configured, the reviewer will check by hand). The name is masked to initials.
/// </summary>
public sealed record NinVerifyResultDto(string Status, string? RegisterName, int LookupsLeft);
