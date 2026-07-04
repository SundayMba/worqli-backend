namespace Servika.Contracts.Reviews;

/// <summary>
/// The customer's review submission for a completed booking. The booking and
/// artisan are taken from the route/booking server-side — never trusted from the
/// client. <see cref="Rating"/> must be 1–5; <see cref="Comment"/> is optional.
/// </summary>
public sealed record SubmitReviewRequest(
    int Rating,
    string? Comment);
