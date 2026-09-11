using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// The assigned artisan submits proof of completed work (photos + optional note):
/// InProgress → AwaitingConfirmation. Photos are stored via <see cref="IFileStorage"/>
/// and their keys saved on the booking. The customer is notified to review & confirm
/// (or it auto-confirms after the window). Scoped to the artisan's own assigned job.
/// </summary>
public sealed class SubmitJobCompletionHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _storage;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public SubmitJobCompletionHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IFileStorage storage,
        NotificationEmitter notifications,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _storage = storage;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, SubmitCompletionRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var photos = request.PhotosBase64 ?? new List<string>();
        if (photos.Count == 0)
            throw new ArgumentException("At least one proof-of-work photo is required.");

        var now = _clock.UtcNow;
        var keys = new List<string>();
        foreach (var b64 in photos.Take(6))
        {
            var bytes = DecodeImage(b64);
            keys.Add(await _storage.SaveAsync(bytes, "image/jpeg", ct));
        }

        string? receiptKey = null;
        if (!string.IsNullOrWhiteSpace(request.ReceiptPhotoBase64))
            receiptKey = await _storage.SaveAsync(DecodeImage(request.ReceiptPhotoBase64), "image/jpeg", ct);

        booking.SubmitCompletion(keys, request.Note, now, receiptKey);
        _notifications.WorkSubmitted(booking);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }

    private static byte[] DecodeImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new ArgumentException("An empty photo was supplied.");
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0 ? base64[(comma + 1)..] : base64;
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new ArgumentException("A photo was not valid base64.");
        }
    }
}
