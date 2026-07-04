using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Disputes;

namespace Servika.Application.Disputes;

/// <summary>
/// The customer's dispute for one of their bookings, or 404 if they haven't raised
/// one. Drives the app's "you reported an issue" state on the booking detail screen.
/// </summary>
public sealed class GetBookingDisputeHandler
{
    private readonly IDisputeRepository _disputes;

    public GetBookingDisputeHandler(IDisputeRepository disputes)
    {
        _disputes = disputes;
    }

    public async Task<DisputeDto> HandleAsync(Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var dispute = await _disputes.FindForBookingAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("No dispute has been raised for this booking.");

        return dispute.ToDto();
    }
}
