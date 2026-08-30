using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Domain.Bookings;

namespace Servika.Application.Admin;

/// <summary>Admin booking oversight — every booking, newest first, optional status
/// filter. Adds the customer's name (joined from users) for the monitor cards.</summary>
public sealed class ListAllBookingsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IUserRepository _users;

    public ListAllBookingsHandler(IBookingRepository bookings, IUserRepository users)
    {
        _bookings = bookings;
        _users = users;
    }

    public async Task<IReadOnlyList<AdminBookingDto>> HandleAsync(string? status, CancellationToken ct)
    {
        BookingStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"'{status}' is not a valid booking status.");
            filter = parsed;
        }

        var bookings = await _bookings.ListAllAsync(filter, ct);
        // One pass over users → a name map (avoids an N+1 of per-booking lookups).
        var names = (await _users.ListAsync(null, ct)).ToDictionary(u => u.Id, u => u.FullName);

        return bookings
            .Select(b => new AdminBookingDto(
                b.Id, b.Status.ToString(), b.ServiceName, b.ArtisanName,
                names.TryGetValue(b.CustomerId, out var n) ? n : "Customer",
                b.AddressText, b.PreferredDate, b.PreferredTimeSlot, b.Urgency.ToString(),
                b.PaymentState.ToString(), b.InitialQuoteAmountNaira, b.CreatedAt,
                b.Assessment.ToString(), b.PaymentMethod.ToString()))
            .ToList();
    }
}

/// <summary>The full admin view of a single booking (unscoped) — parties, money, timeline.</summary>
public sealed class GetAdminBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IUserRepository _users;
    private readonly ICatalogueRepository _catalogue;
    private readonly IBidRepository _bids;

    public GetAdminBookingHandler(
        IBookingRepository bookings, IUserRepository users, ICatalogueRepository catalogue,
        IBidRepository bids)
    {
        _bookings = bookings;
        _users = users;
        _catalogue = catalogue;
        _bids = bids;
    }

    public async Task<AdminBookingDetailDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var b = await _bookings.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"Booking '{id}' was not found.");

        var customer = await _users.FindByIdAsync(b.CustomerId, ct);

        string? artisanPhone = null;
        double? rating = null;
        int? reviews = null;
        if (b.ArtisanId is { } artisanId)
        {
            var profile = await _catalogue.GetArtisanByIdAsync(artisanId, ct);
            if (profile is not null)
            {
                rating = profile.Rating;
                reviews = profile.ReviewCount;
                if (profile.UserId is { } uid)
                    artisanPhone = (await _users.FindByIdAsync(uid, ct))?.PhoneNumber;
            }
        }

        var amount = b.InitialQuoteAmountNaira;
        var commission = amount is { } a ? (int)Math.Round(a * b.CommissionRate) : 0;

        var bids = (await _bids.ListForBookingAsync(b.Id, ct))
            .Select(bid => new AdminBidDto(
                bid.Id, bid.ArtisanName, bid.Status.ToString(), bid.AmountNaira,
                bid.WorkmanshipNaira, bid.MaterialsNaira,
                bid.Materials.Select(m => new Contracts.Bookings.BidMaterialLineDto(m.Name, m.Quantity, m.UnitPriceNaira)).ToList(),
                bid.MaterialsNote, bid.PendingCounterNaira, bid.PendingCounterNote, bid.CounterRounds,
                bid.UpdatedAt))
            .ToList();

        return new AdminBookingDetailDto(
            b.Id, b.Status.ToString(), b.ServiceName, b.CategorySlug, b.Description,
            customer?.FullName ?? "Customer", customer?.Email ?? "", customer?.PhoneNumber ?? "",
            b.ArtisanName, artisanPhone, rating, reviews,
            b.AddressText, b.LocationInstructions,
            b.PreferredDate, b.PreferredTimeSlot, b.Urgency.ToString(),
            amount, commission, b.CommissionRate, b.PaymentState.ToString(),
            b.PaymentMethod.ToString(), b.PricingModel.ToString(), b.Assessment.ToString(),
            b.CreatedAt, b.AcceptedAtUtc, b.WorkSubmittedAtUtc, b.CompletedAtUtc, b.CancelledAtUtc, b.DisputedAtUtc,
            b.AgreedWorkmanshipNaira, b.AgreedMaterialsNaira,
            b.MaterialsAdvanceStatus.ToString(), b.MaterialsAdvanceNaira,
            bids);
    }
}
