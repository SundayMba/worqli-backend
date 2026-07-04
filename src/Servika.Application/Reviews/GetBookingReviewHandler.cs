using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Reviews;

namespace Servika.Application.Reviews;

/// <summary>
/// Returns the review the current customer left for one of their bookings, or 404
/// if they haven't reviewed it. Lets the booking detail screen show "Your review"
/// instead of the "Leave a review" prompt. Scoped to the owner (a booking that
/// isn't theirs simply has no review to return).
/// </summary>
public sealed class GetBookingReviewHandler
{
    private readonly IReviewRepository _reviews;

    public GetBookingReviewHandler(IReviewRepository reviews)
    {
        _reviews = reviews;
    }

    public async Task<ReviewDto> HandleAsync(Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var review = await _reviews.FindForBookingAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("No review has been left for this booking.");

        return review.ToDto();
    }
}
