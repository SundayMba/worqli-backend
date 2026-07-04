using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Reviews;
using Servika.Domain.Bookings;
using Servika.Domain.Reviews;

namespace Servika.Application.Reviews;

/// <summary>
/// The customer reviews a completed booking. Guards (all scoped to the caller):
/// the booking must be theirs (404 if not), <b>Completed</b> (409), have an
/// assigned artisan (409), and not already reviewed (409). On success the review
/// row and the artisan's updated rating aggregate are saved together in one
/// transaction — they share the DbContext, so a single SaveChanges commits both.
/// </summary>
public sealed class SubmitReviewHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IReviewRepository _reviews;
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IClock _clock;

    public SubmitReviewHandler(
        IBookingRepository bookings,
        IReviewRepository reviews,
        ICatalogueRepository catalogue,
        IUserRepository users,
        Notifications.NotificationEmitter notifications,
        IClock clock)
    {
        _bookings = bookings;
        _reviews = reviews;
        _catalogue = catalogue;
        _users = users;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<ReviewDto> HandleAsync(
        Guid customerId, Guid bookingId, SubmitReviewRequest request, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        if (booking.Status is not BookingStatus.Completed)
            throw new ConflictException("You can only review a completed booking.");
        if (booking.ArtisanId is null)
            throw new ConflictException("This booking has no artisan to review.");
        if (await _reviews.ExistsForBookingAsync(bookingId, ct))
            throw new ConflictException("You have already reviewed this booking.");

        // Tracked so its rating aggregate change persists on SaveChanges.
        var artisan = await _catalogue.FindArtisanForUpdateAsync(booking.ArtisanId.Value, ct)
            ?? throw new NotFoundException("The artisan for this booking was not found.");

        var customer = await _users.FindByIdAsync(customerId, ct);

        var review = Review.Create(
            bookingId: booking.Id,
            artisanId: artisan.Id,
            customerId: customerId,
            customerName: customer?.FullName ?? "Customer",
            rating: request.Rating,
            comment: request.Comment,
            serviceName: booking.ServiceName,
            now: _clock.UtcNow);

        _reviews.Add(review);
        artisan.AddRating(request.Rating);
        await _notifications.ArtisanNewReview(booking, request.Rating, ct);
        await _reviews.SaveChangesAsync(ct); // commits the review + the aggregate together

        return review.ToDto();
    }
}
