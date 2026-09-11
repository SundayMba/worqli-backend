namespace Servika.Contracts.Bookings;

/// <summary>
/// Artisan submits proof of completed work (POST /api/v1/artisan/jobs/{id}/submit-completion).
/// Photos are base64 (optionally data: URIs); at least one is required.
/// </summary>
public sealed record SubmitCompletionRequest(
    string? Note,
    IReadOnlyList<string> PhotosBase64,
    /// <summary>Optional materials receipt, sent apart from the work photos.</summary>
    string? ReceiptPhotoBase64 = null);

/// <summary>
/// The artisan's completion proof for a booking (GET /api/v1/bookings/{id}/completion).
/// Photos are returned as base64 data URIs the client renders directly.
/// </summary>
public sealed record JobCompletionDto(
    string Status,
    string? Note,
    DateTimeOffset? SubmittedAtUtc,
    IReadOnlyList<string> Photos,
    /// <summary>The materials receipt as a data URI, or null when none was attached.</summary>
    string? ReceiptPhoto = null);
