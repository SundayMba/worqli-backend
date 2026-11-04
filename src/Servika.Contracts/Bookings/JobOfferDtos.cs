namespace Servika.Contracts.Bookings;

/// <summary>A job offered to the signed-in artisan (GET /api/v1/artisan/jobs/offers). Accept before
/// <see cref="ExpiresAtUtc"/> or it goes to the next artisan.</summary>
public sealed record JobOfferDto(
    Guid Id,
    Guid BookingId,
    /// <summary>1 = first artisan asked.</summary>
    int Round,
    /// <summary>"Rotation" (Servika picked you) or "Direct" (the customer asked for you).</summary>
    string Source,
    DateTimeOffset OfferedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    BookingDetailDto Booking);

/// <summary>Decline an offer (POST /api/v1/artisan/jobs/{id}/offer/decline).</summary>
public sealed record RespondToOfferRequest(string? Reason = null);
