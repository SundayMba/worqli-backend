using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Application.Payments;
using Servika.Contracts.Disputes;
using Servika.Domain.Disputes;

namespace Servika.Application.Disputes;

/// <summary>Lists disputes for the admin queue, newest first, optional status filter.</summary>
public sealed class ListDisputesHandler
{
    private readonly IDisputeRepository _disputes;

    public ListDisputesHandler(IDisputeRepository disputes)
    {
        _disputes = disputes;
    }

    public async Task<IReadOnlyList<DisputeDto>> HandleAsync(string? status, CancellationToken ct)
    {
        DisputeStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DisputeStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"'{status}' is not a valid dispute status.");
            filter = parsed;
        }

        var items = await _disputes.ListAllAsync(filter, ct);
        return items.Select(d => d.ToDto()).ToList();
    }
}

/// <summary>A single dispute by id, for the admin.</summary>
public sealed class GetDisputeHandler
{
    private readonly IDisputeRepository _disputes;

    public GetDisputeHandler(IDisputeRepository disputes)
    {
        _disputes = disputes;
    }

    public async Task<DisputeDto> HandleAsync(Guid disputeId, CancellationToken ct)
    {
        var dispute = await _disputes.FindByIdForUpdateAsync(disputeId, ct)
            ?? throw new NotFoundException($"Dispute '{disputeId}' was not found.");
        return dispute.ToDto();
    }
}

/// <summary>An admin acknowledges a dispute (Open → UnderReview).</summary>
public sealed class MarkDisputeUnderReviewHandler
{
    private readonly IDisputeRepository _disputes;
    private readonly IClock _clock;

    public MarkDisputeUnderReviewHandler(IDisputeRepository disputes, IClock clock)
    {
        _disputes = disputes;
        _clock = clock;
    }

    public async Task<DisputeDto> HandleAsync(Guid adminUserId, Guid disputeId, CancellationToken ct)
    {
        var dispute = await _disputes.FindByIdForUpdateAsync(disputeId, ct)
            ?? throw new NotFoundException($"Dispute '{disputeId}' was not found.");

        dispute.MarkUnderReview(adminUserId, _clock.UtcNow);
        await _disputes.SaveChangesAsync(ct);
        return dispute.ToDto();
    }
}

/// <summary>
/// An admin resolves a dispute with a decision, which also drives the booking to a
/// terminal state (favour customer → Cancelled; favour artisan → Completed) and
/// notifies the customer. Dispute + booking commit together.
/// </summary>
public sealed class ResolveDisputeHandler
{
    private readonly IDisputeRepository _disputes;
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly RefundService _refunds;
    private readonly IClock _clock;

    public ResolveDisputeHandler(
        IDisputeRepository disputes,
        IBookingRepository bookings,
        NotificationEmitter notifications,
        RefundService refunds,
        IClock clock)
    {
        _disputes = disputes;
        _bookings = bookings;
        _notifications = notifications;
        _refunds = refunds;
        _clock = clock;
    }

    public async Task<DisputeDto> HandleAsync(
        Guid adminUserId, Guid disputeId, ResolveDisputeRequest request, CancellationToken ct)
    {
        var resolution = ParseOutcome(request.Outcome);

        var dispute = await _disputes.FindByIdForUpdateAsync(disputeId, ct)
            ?? throw new NotFoundException($"Dispute '{disputeId}' was not found.");

        var now = _clock.UtcNow;
        dispute.Resolve(adminUserId, resolution, request.Note, now);

        // Move the frozen booking to its terminal state.
        var favourCustomer = resolution == DisputeResolution.FavourCustomer;
        var booking = await _bookings.FindByIdAsync(dispute.BookingId, ct);
        if (booking is not null)
        {
            booking.ResolveDispute(favourCustomer, now);
            _notifications.DisputeResolved(booking, favourCustomer);
            // Favour-customer → refund the escrow (no-op if the booking was never paid).
            if (favourCustomer)
                await _refunds.RefundIfPaidAsync(booking, ct);
        }

        await _disputes.SaveChangesAsync(ct); // commits dispute + booking + refund + notifications
        return dispute.ToDto();
    }

    private static DisputeResolution ParseOutcome(string? outcome) => (outcome ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "customer" or "favourcustomer" or "favorcustomer" => DisputeResolution.FavourCustomer,
        "artisan" or "favourartisan" or "favorartisan" => DisputeResolution.FavourArtisan,
        _ => throw new ArgumentException("Outcome must be 'customer' or 'artisan'."),
    };
}
