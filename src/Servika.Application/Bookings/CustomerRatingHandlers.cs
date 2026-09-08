using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Rate the customer (design 61). Allowed once the artisan's work is submitted
/// or the job is complete; one rating per booking; private to Servika.
/// </summary>
public sealed class RateCustomerHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly ICustomerRatingRepository _ratings;
    private readonly IClock _clock;

    public RateCustomerHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue,
        ICustomerRatingRepository ratings, IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _ratings = ratings;
        _clock = clock;
    }

    public async Task<CustomerRatingDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, RateCustomerRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        if (booking.Status is not (BookingStatus.Completed or BookingStatus.AwaitingConfirmation or BookingStatus.Disputed))
            throw new ConflictException("You can rate the customer once the work is done.");
        if (await _ratings.FindByBookingAsync(bookingId, ct) is not null)
            throw new ConflictException("You already rated this customer for this job.");

        var rating = CustomerRating.Create(
            booking.Id, booking.CustomerId, artisanUserId, request.Stars, request.Tags, request.PrivateNote, _clock.UtcNow);
        _ratings.Add(rating);
        await _ratings.SaveChangesAsync(ct);
        return rating.ToDto();
    }
}

public sealed class GetCustomerRatingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly ICustomerRatingRepository _ratings;

    public GetCustomerRatingHandler(IBookingRepository bookings, ICatalogueRepository catalogue, ICustomerRatingRepository ratings)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _ratings = ratings;
    }

    public async Task<CustomerRatingDto> HandleAsync(Guid artisanUserId, Guid bookingId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");
        _ = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");
        var rating = await _ratings.FindByBookingAsync(bookingId, ct)
            ?? throw new NotFoundException("Not rated yet.");
        return rating.ToDto();
    }
}

internal static class CustomerRatingMapping
{
    public static CustomerRatingDto ToDto(this CustomerRating r) =>
        new(r.BookingId, r.Stars, r.Tags, r.PrivateNote, r.CreatedAt);
}
