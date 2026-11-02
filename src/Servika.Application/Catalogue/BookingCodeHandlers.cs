using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Resolve a booking code to the little a customer needs to book that artisan.</summary>
public sealed class GetArtisanByCodeHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IBookingRepository _bookings;
    private readonly IClock _clock;

    public GetArtisanByCodeHandler(ICatalogueRepository catalogue, IBookingRepository bookings, IClock clock)
    {
        _catalogue = catalogue;
        _bookings = bookings;
        _clock = clock;
    }

    public async Task<ArtisanByCodeDto> HandleAsync(string code, CancellationToken ct)
    {
        var normalized = BookingCodes.Normalize(code);
        if (normalized.Length < 3) throw new NotFoundException("No artisan has that code.");
        var artisan = await _catalogue.FindArtisanByBookingCodeAsync(normalized, ct);
        if (artisan is null || artisan.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new NotFoundException("No artisan has that code.");
        var completed = await _bookings.CountCompletedForArtisansAsync(new[] { artisan.Id }, ct);
        return new ArtisanByCodeDto(
            artisan.Id,
            artisan.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? artisan.FullName,
            artisan.FullName, artisan.Specialty, string.IsNullOrEmpty(artisan.PhotoKey) ? null : $"/api/v1/artisans/{artisan.Id}/photo", artisan.Location,
            artisan.IsAvailable, artisan.IsAway(_clock.UtcNow),
            completed.TryGetValue(artisan.Id, out var n) ? n : 0,
            artisan.CategorySlugs, artisan.BookingCode ?? normalized);
    }
}

/// <summary>The artisans this customer has completed jobs with, most recent first ("Book again").</summary>
public sealed class GetPastArtisansHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IBookingRepository _bookings;
    private readonly IClock _clock;

    public GetPastArtisansHandler(ICatalogueRepository catalogue, IBookingRepository bookings, IClock clock)
    {
        _catalogue = catalogue;
        _bookings = bookings;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PastArtisanDto>> HandleAsync(Guid customerId, CancellationToken ct)
    {
        var stats = await _bookings.ListCompletedArtisansForCustomerAsync(customerId, ct);
        if (stats.Count == 0) return Array.Empty<PastArtisanDto>();
        var artisans = (await _catalogue.ListArtisansByIdsAsync(stats.Select(s => s.ArtisanId).ToList(), ct))
            .ToDictionary(a => a.Id);
        var now = _clock.UtcNow;
        return stats
            .Where(s => artisans.ContainsKey(s.ArtisanId))
            .OrderByDescending(s => s.LastCompletedAtUtc)
            .Select(s =>
            {
                var a = artisans[s.ArtisanId];
                return new PastArtisanDto(a.Id, a.FullName, a.Specialty, a.ImageKey, string.IsNullOrEmpty(a.PhotoKey) ? null : $"/api/v1/artisans/{a.Id}/photo", a.CategorySlugs,
                    s.Jobs, s.LastCompletedAtUtc, a.IsAvailable, a.IsAway(now),
                    a.VerificationStatus == ArtisanVerificationStatus.Verified && a.UserId is not null);
            })
            .ToList();
    }
}

/// <summary>Gives a verified artisan their booking code the first time it is needed.</summary>
public sealed class BookingCodeAssigner
{
    private readonly ICatalogueRepository _catalogue;

    public BookingCodeAssigner(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    /// <summary>Assigns a unique code when the profile is verified and has none. Stages the change; caller saves.</summary>
    public async Task<bool> EnsureAsync(ArtisanProfile profile, CancellationToken ct)
    {
        if (profile.BookingCode is not null || profile.VerificationStatus != ArtisanVerificationStatus.Verified) return false;
        string code;
        var tries = 0;
        do
        {
            code = BookingCodes.Generate(profile.FullName);
            if (++tries > 20) code = BookingCodes.Generate(profile.FullName) + Random.Shared.Next(100, 999);
        }
        while (await _catalogue.BookingCodeExistsAsync(code, ct));
        profile.SetBookingCode(code);
        return true;
    }
}
