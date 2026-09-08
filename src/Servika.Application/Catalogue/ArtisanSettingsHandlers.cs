using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Saves the payout account withdrawals default to (verification check 5 / design 63).</summary>
public sealed class SetPayoutAccountHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanGuarantorRepository _guarantors;

    public SetPayoutAccountHandler(ICatalogueRepository catalogue, IArtisanGuarantorRepository guarantors)
    {
        _catalogue = catalogue;
        _guarantors = guarantors;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(
        Guid artisanUserId, SetPayoutAccountRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");
        profile.SetPayoutAccount(request.BankCode, request.BankName, request.AccountNumber, request.AccountName);
        await _catalogue.SaveChangesAsync(ct);
        return profile.ToMyProfileDto(await _guarantors.CountForUserAsync(artisanUserId, ct));
    }
}

/// <summary>Radius, emergency opt-in and working hours (design 49).</summary>
public sealed class SetWorkPreferencesHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanGuarantorRepository _guarantors;

    public SetWorkPreferencesHandler(ICatalogueRepository catalogue, IArtisanGuarantorRepository guarantors)
    {
        _catalogue = catalogue;
        _guarantors = guarantors;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(
        Guid artisanUserId, SetWorkPreferencesRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");
        if (request.WorkingHoursJson is { Length: > 2000 })
            throw new ArgumentException("Working hours are too long.");
        profile.SetWorkPreferences(request.RadiusKm, request.AcceptsEmergency, request.WorkingHoursJson);
        await _catalogue.SaveChangesAsync(ct);
        return profile.ToMyProfileDto(await _guarantors.CountForUserAsync(artisanUserId, ct));
    }
}

/// <summary>Away mode (design 64): hidden from search until a date; accepted jobs untouched.</summary>
public sealed class SetAwayHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IClock _clock;

    public SetAwayHandler(ICatalogueRepository catalogue, IArtisanGuarantorRepository guarantors, IClock clock)
    {
        _catalogue = catalogue;
        _guarantors = guarantors;
        _clock = clock;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(
        Guid artisanUserId, SetAwayRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Set up your Pro profile first.");
        if (request.UntilUtc is { } until && until <= _clock.UtcNow)
            throw new ArgumentException("Pick a date in the future, or clear away mode.");
        profile.SetAway(request.UntilUtc);
        // Away = not available to new customers. Coming back restores availability.
        profile.SetAvailability(request.UntilUtc is null);
        await _catalogue.SaveChangesAsync(ct);
        return profile.ToMyProfileDto(await _guarantors.CountForUserAsync(artisanUserId, ct));
    }
}

/// <summary>The artisan's guarantors: list, add (with optional ID photo), remove.</summary>
public sealed class ListGuarantorsHandler
{
    private readonly IArtisanGuarantorRepository _guarantors;
    public ListGuarantorsHandler(IArtisanGuarantorRepository guarantors) => _guarantors = guarantors;

    public async Task<IReadOnlyList<GuarantorDto>> HandleAsync(Guid artisanUserId, CancellationToken ct) =>
        (await _guarantors.ListForUserAsync(artisanUserId, ct)).Select(g => g.ToDto()).ToList();
}

public sealed class AddGuarantorHandler
{
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IFileStorage _files;
    private readonly IClock _clock;

    public AddGuarantorHandler(IArtisanGuarantorRepository guarantors, IFileStorage files, IClock clock)
    {
        _guarantors = guarantors;
        _files = files;
        _clock = clock;
    }

    public async Task<GuarantorDto> HandleAsync(Guid artisanUserId, AddGuarantorRequest request, CancellationToken ct)
    {
        if (await _guarantors.CountForUserAsync(artisanUserId, ct) >= 4)
            throw new ConflictException("You can list up to four guarantors.");

        string? photoKey = null;
        if (!string.IsNullOrWhiteSpace(request.IdPhotoBase64))
        {
            var payload = request.IdPhotoBase64!;
            var comma = payload.IndexOf(',');
            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
                payload = payload[(comma + 1)..];
            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException) { throw new ArgumentException("The ID photo isn't a valid image upload."); }
            photoKey = await _files.SaveAsync(bytes, "image/jpeg", ct);
        }

        var g = ArtisanGuarantor.Create(
            artisanUserId, request.FullName, request.Phone, request.Relationship,
            request.YearsKnown, request.Occupation, request.Address, photoKey, _clock.UtcNow);
        _guarantors.Add(g);
        await _guarantors.SaveChangesAsync(ct);
        return g.ToDto();
    }
}

public sealed class RemoveGuarantorHandler
{
    private readonly IArtisanGuarantorRepository _guarantors;
    public RemoveGuarantorHandler(IArtisanGuarantorRepository guarantors) => _guarantors = guarantors;

    public async Task HandleAsync(Guid artisanUserId, Guid id, CancellationToken ct)
    {
        var g = await _guarantors.FindForUserAsync(id, artisanUserId, ct)
            ?? throw new NotFoundException("That guarantor was not found.");
        _guarantors.Remove(g);
        await _guarantors.SaveChangesAsync(ct);
    }
}

internal static class GuarantorMapping
{
    public static GuarantorDto ToDto(this ArtisanGuarantor g) =>
        new(g.Id, g.FullName, g.Phone, g.Relationship, g.YearsKnown, g.Occupation, g.Address,
            !string.IsNullOrEmpty(g.IdPhotoKey), g.CreatedAt);
}
