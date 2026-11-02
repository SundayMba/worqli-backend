using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;


/// <summary>
/// The artisan's own proof-of-work for one of their assigned jobs — the note +
/// photos they sent (as data URIs). Powers the Pro job receipt. Scoped to the
/// assigned artisan like every other artisan job read.
/// </summary>
public sealed class GetArtisanJobCompletionHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _storage;
    private readonly IPlatformSettingsRepository _settings;

    public GetArtisanJobCompletionHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue, IFileStorage storage,
        IPlatformSettingsRepository settings)
    {
        _settings = settings;
        _bookings = bookings;
        _catalogue = catalogue;
        _storage = storage;
    }

    public async Task<JobCompletionDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var photos = new List<string>();
        foreach (var key in booking.CompletionPhotoKeys)
        {
            var file = await _storage.GetAsync(key, ct);
            if (file is not null)
                photos.Add($"data:{file.ContentType};base64,{Convert.ToBase64String(file.Content)}");
        }

        string? receipt = null;
        if (booking.CompletionReceiptKey is not null)
        {
            var r = await _storage.GetAsync(booking.CompletionReceiptKey, ct);
            if (r is not null) receipt = $"data:{r.ContentType};base64,{Convert.ToBase64String(r.Content)}";
        }

        return new JobCompletionDto(
            booking.Status.ToString(), booking.CompletionNote, booking.WorkSubmittedAtUtc, photos, receipt,
            booking.AutoConfirmAtUtc((await _settings.GetOrCreateAsync(ct)).AutoConfirmHours));
    }
}
