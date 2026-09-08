using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>
/// One OPEN request in full, for an artisan deciding whether to claim or quote it
/// (the Pro "Job detail" screen). Only verified artisans whose categories include
/// the job's may read it; anything else is a 404, never a leak. The exact address
/// is deliberately NOT withheld here because the summary already carries it; the
/// customer's name and completed-job count give the artisan someone to trust.
/// </summary>
public sealed class GetOpenJobDetailHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly IBidRepository _bids;

    public GetOpenJobDetailHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IUserRepository users,
        IBidRepository bids)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _users = users;
        _bids = bids;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null || profile.VerificationStatus != ArtisanVerificationStatus.Verified)
            throw new NotFoundException("This request was not found.");

        var booking = await _bookings.FindByIdReadOnlyAsync(bookingId, ct);
        if (booking is null
            || booking.Status != BookingStatus.Open
            || !profile.CategorySlugs.Contains(booking.CategorySlug))
            throw new NotFoundException("This request was not found.");

        var customer = await _users.FindByIdAsync(booking.CustomerId, ct);
        var completed = await _bookings.CountCompletedForCustomerAsync(booking.CustomerId, ct);
        var bidCount = booking.Assessment == AssessmentMode.RemoteQuote
            ? await _bids.CountActiveForBookingAsync(booking.Id, ct)
            : 0;

        return booking.ToDetailDto(bidCount, customer?.FullName, completed);
    }
}

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

    public GetArtisanJobCompletionHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue, IFileStorage storage)
    {
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

        return new JobCompletionDto(
            booking.Status.ToString(), booking.CompletionNote, booking.WorkSubmittedAtUtc, photos);
    }
}
