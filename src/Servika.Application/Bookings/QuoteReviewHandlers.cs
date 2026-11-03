using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Application.Payments;
using Servika.Contracts.Admin;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Settings;

namespace Servika.Application.Bookings;

/// <summary>
/// When ops must check a custom quote before the customer sees it (Business Model v1.4, section 10): the total
/// is at or above the review threshold, or the labour is above the range factor times the top of the typical
/// range for that kind of job. Pure, so the rules are unit-tested. An empty list means it goes straight out.
/// </summary>
public static class QuoteReviewRules
{
    public static IReadOnlyList<string> Reasons(Booking booking, Bid bid, PlatformSettings settings)
    {
        var reasons = new List<string>();
        if (bid.AmountNaira >= settings.QuoteReviewThresholdNaira)
            reasons.Add($"Total ₦{bid.AmountNaira:N0} is at or above the ₦{settings.QuoteReviewThresholdNaira:N0} check line");
        if (booking.TypicalRangeMaxNaira is { } max && bid.WorkmanshipNaira > max * settings.QuoteReviewRangeFactor)
            reasons.Add($"Labour ₦{bid.WorkmanshipNaira:N0} is more than {settings.QuoteReviewRangeFactor:0.##}× the typical top of ₦{max:N0}");
        return reasons;
    }
}

/// <summary>Custom quotes waiting for ops, oldest first, with what makes each one need a look.</summary>
public sealed class ListQuoteReviewsHandler
{
    private readonly IBidRepository _bids;
    private readonly IBookingRepository _bookings;
    private readonly IUserRepository _users;
    private readonly IPlatformSettingsRepository _settings;

    public ListQuoteReviewsHandler(IBidRepository bids, IBookingRepository bookings, IUserRepository users, IPlatformSettingsRepository settings)
    {
        _bids = bids;
        _bookings = bookings;
        _users = users;
        _settings = settings;
    }

    public async Task<IReadOnlyList<AdminQuoteReviewDto>> HandleAsync(CancellationToken ct)
    {
        var settings = await _settings.GetOrCreateAsync(ct);
        var result = new List<AdminQuoteReviewDto>();
        foreach (var bid in await _bids.ListAwaitingReviewAsync(ct))
        {
            var booking = await _bookings.FindByIdReadOnlyAsync(bid.BookingId, ct);
            if (booking is null) continue;
            var customer = await _users.FindByIdAsync(booking.CustomerId, ct);
            result.Add(new AdminQuoteReviewDto(
                bid.Id, booking.Id, booking.ServiceName, booking.CategorySlug, booking.Description, booking.AddressText,
                customer?.FullName ?? "Customer", bid.ArtisanName,
                bid.WorkmanshipNaira, bid.MaterialsNaira, bid.AmountNaira,
                bid.Materials.Select(m => new BidMaterialLineDto(m.Name, m.Quantity, m.UnitPriceNaira)).ToList(),
                bid.MaterialsNote, booking.TypicalRangeMinNaira, booking.TypicalRangeMaxNaira, booking.DiagnosisFeeNaira ?? 0,
                QuoteReviewRules.Reasons(booking, bid, settings), bid.UpdatedAt));
        }
        return result;
    }
}

/// <summary>Ops pass a custom quote (the customer sees it) or send it back with a note (the artisan revises).</summary>
public sealed class DecideQuoteReviewHandler
{
    private readonly IBidRepository _bids;
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public DecideQuoteReviewHandler(IBidRepository bids, IBookingRepository bookings, NotificationEmitter notifications, IClock clock)
    {
        _bids = bids;
        _bookings = bookings;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task HandleAsync(Guid adminUserId, Guid bidId, DecideQuoteReviewRequest request, CancellationToken ct)
    {
        var bid = await _bids.FindByIdAsync(bidId, ct) ?? throw new NotFoundException("That quote was not found.");
        var booking = await _bookings.FindByIdReadOnlyAsync(bid.BookingId, ct) ?? throw new NotFoundException("That job was not found.");
        if (bid.Status != BidStatus.Active || booking.HasAcceptedCustomQuote || booking.Status != BookingStatus.Arrived)
            throw new ConflictException("This quote is no longer open.");

        var now = _clock.UtcNow;
        switch (request.Outcome?.Trim().ToLowerInvariant())
        {
            case "approve":
                bid.ApproveReview(adminUserId, now);
                _notifications.BidPlaced(booking, bid);
                break;
            case "sendback":
            case "send-back":
                bid.SendBackForChanges(adminUserId, request.Note ?? string.Empty, now);
                await _notifications.QuoteSentBackAsync(booking, bid, ct);
                break;
            default:
                throw new ArgumentException("Outcome must be 'approve' or 'sendback'.");
        }
        await _bids.SaveChangesAsync(ct);
    }
}

/// <summary>
/// The customer does not go ahead with a custom quote: they pay only the visit, which the artisan earns, and the
/// job closes. They can book a second opinion.
/// </summary>
public sealed class DeclineCustomQuoteHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBidRepository _bids;
    private readonly EscrowReleaseService _escrow;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public DeclineCustomQuoteHandler(IBookingRepository bookings, IBidRepository bids, EscrowReleaseService escrow,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _bids = bids;
        _escrow = escrow;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("This booking was not found.");
        if (!booking.IsCustomQuote || !booking.OnlyVisitAgreed || booking.Status != BookingStatus.Arrived)
            throw new ConflictException("There is no quote to decline on this booking.");

        var now = _clock.UtcNow;
        foreach (var bid in await _bids.ListForBookingAsync(booking.Id, ct)) bid.MarkClosed(now);
        booking.CompleteAsDiagnosisOnly(now);
        await _escrow.ReleaseIfPaidAsync(booking, ct);
        await _notifications.RepairsDecidedAsync(booking, 0, ct);
        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto(0, null, null);
    }
}
