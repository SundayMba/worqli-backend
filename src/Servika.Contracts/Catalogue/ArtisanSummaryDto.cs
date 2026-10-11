namespace Servika.Contracts.Catalogue;

/// <summary>
/// The compact artisan card shown in the home "Nearby Artisans" carousel and the
/// category listing (GET /api/v1/artisans, GET /api/v1/categories/{slug}/artisans).
/// The client resolves the avatar from <see cref="ImageKey"/>.
/// </summary>
public sealed record ArtisanSummaryDto(
    Guid Id,
    string ImageKey,
    string FullName,
    string Specialty,
    double Rating,
    int ReviewCount,
    /// <summary>Km from the caller's coords; null when the caller's or the artisan's
    /// location is unknown.</summary>
    double? DistanceKm,
    bool IsAvailable,
    string Accent,
    double? Latitude,
    double? Longitude,
    /// <summary>API path of the artisan's uploaded photo (e.g.
    /// "/api/v1/artisans/{id}/photo"), or null if they haven't uploaded one —
    /// the client then falls back to the bundled <see cref="ImageKey"/> art.</summary>
    string? PhotoUrl,
    /// <summary>True when the artisan uploaded a work certificate (trust badge;
    /// also boosts their ranking while ratings accumulate).</summary>
    bool HasCertificate,
    /// <summary>True when the artisan passed verification.</summary>
    bool IsVerified,
    /// <summary>Real count of completed jobs on Servika.</summary>
    int CompletedJobs);
