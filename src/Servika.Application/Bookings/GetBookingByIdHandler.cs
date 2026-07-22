using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Returns one booking's full detail, but only if it belongs to the requesting
/// customer — another customer's id is a 404, never a leak.
/// </summary>
public sealed class GetBookingByIdHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;

    public GetBookingByIdHandler(IBookingRepository bookings, IBidRepository bids)
    {
        _bookings = bookings;
        _bids = bids;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        // Bid badge matters while offers can still arrive: an open RemoteQuote
        // broadcast, a direct Pending request awaiting its artisan's quote, or
        // an unpaid inspect-first job whose artisan may quote en-route/on-site.
        var bidCount =
            (booking.Status == Domain.Bookings.BookingStatus.Open &&
             booking.Assessment == Domain.Bookings.AssessmentMode.RemoteQuote)
            || (booking.ArtisanId is not null &&
                booking.PaymentState != Domain.Bookings.BookingPaymentState.Paid &&
                booking.Status is Domain.Bookings.BookingStatus.Pending
                    or Domain.Bookings.BookingStatus.Accepted
                    or Domain.Bookings.BookingStatus.OnMyWay
                    or Domain.Bookings.BookingStatus.Arrived)
                ? await _bids.CountActiveForBookingAsync(bookingId, ct)
                : 0;

        return booking.ToDetailDto(bidCount);
    }
}
