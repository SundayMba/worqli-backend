namespace Servika.Contracts.Bookings;

/// <summary>POST /api/v1/artisan/jobs/{id}/rate-customer — the artisan's private
/// rating of the customer after a job. Never shown to the customer.</summary>
public sealed record RateCustomerRequest(int Stars, List<string>? Tags, string? PrivateNote);

/// <summary>The artisan's own rating of a customer for one job (GET …/customer-rating; 404 = not rated yet).</summary>
public sealed record CustomerRatingDto(
    Guid BookingId,
    int Stars,
    IReadOnlyList<string> Tags,
    string? PrivateNote,
    DateTimeOffset CreatedAt);
