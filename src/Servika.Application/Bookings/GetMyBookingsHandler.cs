using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Lists the signed-in customer's bookings (their history), newest first, with an
/// optional status filter. An unrecognised status string is a 400 rather than a
/// silently-empty list.
/// </summary>
public sealed class GetMyBookingsHandler
{
    private readonly IBookingRepository _bookings;

    public GetMyBookingsHandler(IBookingRepository bookings)
    {
        _bookings = bookings;
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> HandleAsync(
        Guid customerId, string? status, CancellationToken ct)
    {
        BookingStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unknown booking status '{status}'.", nameof(status));
            filter = parsed;
        }

        var bookings = await _bookings.ListForCustomerAsync(customerId, filter, ct);
        return bookings.Select(b => b.ToSummaryDto()).ToList();
    }
}
