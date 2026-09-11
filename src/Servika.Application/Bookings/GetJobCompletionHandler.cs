using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Returns the artisan's proof-of-work for one of the customer's bookings — the
/// note + photos (as base64 data URIs the app renders). Scoped to the booking's
/// owner. Powers the customer's "review & confirm" screen.
/// </summary>
public sealed class GetJobCompletionHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IFileStorage _storage;

    public GetJobCompletionHandler(IBookingRepository bookings, IFileStorage storage)
    {
        _bookings = bookings;
        _storage = storage;
    }

    public async Task<JobCompletionDto> HandleAsync(Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
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
            booking.Status.ToString(), booking.CompletionNote, booking.WorkSubmittedAtUtc, photos, receipt);
    }
}
