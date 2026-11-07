using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The availability line on Home: checked artisans who would see a job posted at this point (not away,
/// base within the open pool's reach, <see cref="Bookings.OpenJobMatching.PoolMaxKm"/>) and how many of
/// them are online now. Counts only, no names.
/// </summary>
public sealed class GetAvailabilityHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;

    public GetAvailabilityHandler(ICatalogueRepository catalogue, IClock clock)
    {
        _catalogue = catalogue;
        _clock = clock;
    }

    public async Task<AvailabilityDto> HandleAsync(double? lat, double? lng, string? categorySlug, CancellationToken ct)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180) throw new ArgumentException("That location is not valid.");
        var slug = string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim().ToLowerInvariant();
        var now = _clock.UtcNow;
        var candidates = await _catalogue.ListCoverageCandidatesAsync(slug, ct);
        int available = 0, covering = 0;
        foreach (var c in candidates)
        {
            if (c.Rules.AwayUntilUtc is { } away && away > now) continue;
            if (!Covers(c.Rules, lat, lng)) continue;
            covering++;
            if (c.IsAvailable) available++;
        }
        return new AvailabilityDto(available, covering);
    }

    /// <summary>The artisan would see a job at this point: their base is within the open pool's reach (the
    /// same rule the open-jobs list uses; their alert radius does not matter). With no point given every
    /// artisan counts; an artisan with no pinned base does not count for a known point (Home must never
    /// claim coverage it cannot show).</summary>
    public static bool Covers(ArtisanWorkRules rules, double? lat, double? lng)
    {
        if (lat is not { } la || lng is not { } ln) return true;
        if (rules.Latitude is not { } aLat || rules.Longitude is not { } aLng) return false;
        return GeoDistance.Km(la, ln, aLat, aLng) <= Bookings.OpenJobMatching.PoolMaxKm;
    }
}

/// <summary>Records "Tell us what you need" for a coming-soon service or an uncovered area.</summary>
public sealed class RecordServiceInterestHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;
    private readonly IAdminAlerts _alerts;

    public RecordServiceInterestHandler(ICatalogueRepository catalogue, IClock clock, IAdminAlerts alerts)
    {
        _catalogue = catalogue;
        _clock = clock;
        _alerts = alerts;
    }

    public async Task HandleAsync(Guid? userId, RecordServiceInterestRequest request, CancellationToken ct)
    {
        var interest = ServiceInterest.Record(
            request.CategorySlug, request.Note, request.AreaLabel, request.Latitude, request.Longitude, userId, _clock.UtcNow);
        _catalogue.AddServiceInterest(interest);
        await _catalogue.SaveChangesAsync(ct);

        // Tell ops after it is saved. The note is the customer's own words, so phone numbers and emails are hidden
        // (alerts never carry personal details) and it is cut short.
        var service = interest.CategorySlug is { } slug
            ? (await _catalogue.GetCategoryBySlugAsync(slug, ct))?.Name ?? slug
            : null;
        var lines = new List<string>();
        if (service is not null) lines.Add($"Service: {service}");
        if (interest.Note is { } note)
        {
            var safe = Chat.ChatContentPolicy.Redact(note);
            lines.Add($"They wrote: \"{(safe.Length > 140 ? safe[..140] + "…" : safe)}\"");
        }
        lines.Add($"Area: {interest.AreaLabel ?? "not given"} · {(userId is null ? "guest" : "signed in")}");
        _alerts.Send(new AdminAlert(AdminAlertKind.ServiceRequest, "Someone asked for a service", lines,
            "/service-requests", "See all requests"));
    }
}

/// <summary>The latest demand requests, newest first (admin).</summary>
public sealed class ListServiceInterestsHandler
{
    private readonly ICatalogueRepository _catalogue;

    public ListServiceInterestsHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<ServiceInterestDto>> HandleAsync(CancellationToken ct) =>
        (await _catalogue.ListServiceInterestsAsync(200, ct))
            .Select(s => new ServiceInterestDto(s.Id, s.CategorySlug, s.Note, s.AreaLabel, s.Latitude, s.Longitude, s.UserId is not null, s.CreatedAtUtc))
            .ToList();
}
