using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Application.Payments;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Bookings;

/// <summary>Maps repairs and the money picture of a job.</summary>
public static class RepairMapping
{
    public static BookingRepairDto ToDto(this BookingRepair r) =>
        new(r.Id, r.PriceBookItemId, r.Round, r.Name, r.LabourNaira, r.PartsNaira, r.PartsNote, r.TotalNaira,
            $"/api/v1/bookings/{r.BookingId}/repairs/{r.Id}/photo", r.Status.ToString(), r.ProposedAtUtc);

    public static BookingRepairsDto ToRepairsDto(this Booking b, IEnumerable<BookingRepair> repairs) =>
        new(repairs.Select(r => r.ToDto()).ToList(), b.DiagnosisFeeNaira, b.ApprovedRepairsNaira,
            b.InitialQuoteAmountNaira ?? 0, b.PaidNaira, b.AmountDueNaira);
}

/// <summary>
/// The artisan, at the job, picks the repairs it needs from Servika's add-on list, each with a photo of the
/// fault (Business Model v1.4, section 7). The artisan chooses WHICH repairs, never what they cost: labour is
/// Servika's price, parts are itemised beside it. Only on a job paid upfront (a diagnosis visit, or a fixed job
/// that turned out bigger), once the artisan is there, with everything approved so far paid for.
/// </summary>
public sealed class ProposeRepairsHandler
{
    public const int MaxItems = 8;

    private readonly IBookingRepository _bookings;
    private readonly IBookingRepairRepository _repairs;
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public ProposeRepairsHandler(IBookingRepository bookings, IBookingRepairRepository repairs, IPriceBookRepository priceBook,
        ICatalogueRepository catalogue, IFileStorage files, NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _repairs = repairs;
        _priceBook = priceBook;
        _catalogue = catalogue;
        _files = files;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingRepairsDto> HandleAsync(Guid artisanUserId, Guid bookingId, ProposeRepairsRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("This job was not found.");
        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException("This job was not found.");

        if (!booking.PaysUpfront)
            throw new ConflictException("This job is priced with your quote, not from Servika's list. Send a quote instead.");
        if (booking.Status is not (BookingStatus.Arrived or BookingStatus.InProgress))
            throw new ConflictException("Repairs are added once you are at the job.");
        if (booking.PaidNaira <= 0 || booking.AmountDueNaira > 0)
            throw new ConflictException("Wait until the customer has paid for what is already agreed.");

        var existing = await _repairs.ListForBookingAsync(booking.Id, ct);
        if (existing.Any(r => r.Status == BookingRepairStatus.Proposed))
            throw new ConflictException("The customer has not answered your last list yet.");

        var items = request.Items ?? new();
        if (items.Count == 0) throw new ArgumentException("Pick at least one repair from the list.");
        if (items.Count > MaxItems) throw new ArgumentException($"Send at most {MaxItems} repairs at a time.");
        if (items.Select(i => i.PriceBookItemId).Distinct().Count() != items.Count)
            throw new ArgumentException("Each repair can be picked once. Use the parts note for quantities.");

        // Resolve every line against the price book before storing anything.
        var lines = new List<(PriceBookItem Item, ProposeRepairItem Request)>();
        foreach (var i in items)
        {
            var item = await _priceBook.FindAsync(i.PriceBookItemId, ct);
            if (item is not { IsActive: true, Kind: PriceBookItemKind.AddOn, PriceNaira: > 0 } || item.CategorySlug != booking.CategorySlug)
                throw new ArgumentException("One of those repairs is not on Servika's list for this job. Pick again.");
            if (string.IsNullOrWhiteSpace(i.PhotoBase64))
                throw new ArgumentException($"Add a photo of the fault for \"{item.Name}\".");
            lines.Add((item, i));
        }

        var round = existing.Count == 0 ? 1 : existing.Max(r => r.Round) + 1;
        var now = _clock.UtcNow;
        var added = new List<BookingRepair>();
        foreach (var (item, i) in lines)
        {
            var key = await _files.SaveAsync(DecodePhoto(i.PhotoBase64, item.Name), "image/jpeg", ct);
            var repair = BookingRepair.Propose(booking.Id, item.Id, round, item.Name, item.PriceNaira!.Value,
                i.PartsNaira, i.PartsNote, key, now);
            _repairs.Add(repair);
            added.Add(repair);
        }

        _notifications.RepairsProposed(booking, added.Count, added.Sum(r => r.TotalNaira));
        await _repairs.SaveChangesAsync(ct);
        return booking.ToRepairsDto(existing.Concat(added));
    }

    private static byte[] DecodePhoto(string base64, string name)
    {
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0 ? base64[(comma + 1)..] : base64;
        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0) throw new ArgumentException($"The photo for \"{name}\" is empty.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new ArgumentException($"The photo for \"{name}\" could not be read.");
        }
    }
}

/// <summary>The repairs on a job and the money picture, for its customer or its assigned artisan.</summary>
public sealed class GetBookingRepairsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBookingRepairRepository _repairs;
    private readonly ICatalogueRepository _catalogue;

    public GetBookingRepairsHandler(IBookingRepository bookings, IBookingRepairRepository repairs, ICatalogueRepository catalogue)
    {
        _bookings = bookings;
        _repairs = repairs;
        _catalogue = catalogue;
    }

    public async Task<BookingRepairsDto> HandleAsync(Guid userId, Guid bookingId, CancellationToken ct)
    {
        var booking = await BookingParties.FindForPartyAsync(_bookings, _catalogue, userId, bookingId, ct);
        return booking.ToRepairsDto(await _repairs.ListForBookingAsync(booking.Id, ct));
    }
}

/// <summary>Serves a repair's fault photo to the job's customer or assigned artisan.</summary>
public sealed class GetRepairPhotoHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBookingRepairRepository _repairs;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _files;

    public GetRepairPhotoHandler(IBookingRepository bookings, IBookingRepairRepository repairs, ICatalogueRepository catalogue, IFileStorage files)
    {
        _bookings = bookings;
        _repairs = repairs;
        _catalogue = catalogue;
        _files = files;
    }

    public async Task<StoredFile> HandleAsync(Guid userId, Guid bookingId, Guid repairId, CancellationToken ct)
    {
        await BookingParties.FindForPartyAsync(_bookings, _catalogue, userId, bookingId, ct);
        var repair = await _repairs.FindAsync(bookingId, repairId, ct) ?? throw new NotFoundException("That repair was not found.");
        return await _files.GetAsync(repair.PhotoKey, ct) ?? throw new NotFoundException("The photo file was not found.");
    }
}

/// <summary>Who may see a job: its customer, or the artisan assigned to it. Everyone else gets a 404.</summary>
public static class BookingParties
{
    public static async Task<Booking> FindForPartyAsync(
        IBookingRepository bookings, ICatalogueRepository catalogue, Guid userId, Guid bookingId, CancellationToken ct)
    {
        var booking = await bookings.FindByIdReadOnlyAsync(bookingId, ct) ?? throw new NotFoundException("This job was not found.");
        if (booking.CustomerId == userId) return booking;
        var profile = await catalogue.GetArtisanByUserIdAsync(userId, ct);
        if (profile is not null && booking.ArtisanId == profile.Id) return booking;
        throw new NotFoundException("This job was not found.");
    }
}

/// <summary>
/// The customer answers the proposed repairs: the ones they approve are added to the job at Servika's prices
/// (a diagnosis fee is credited against them) and must be paid before the work starts; the rest are declined.
/// Declining everything after a diagnosis ends the job there: the customer has paid only the visit, which the
/// artisan earns.
/// </summary>
public sealed class DecideRepairsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IBookingRepairRepository _repairs;
    private readonly EscrowReleaseService _escrow;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public DecideRepairsHandler(IBookingRepository bookings, IBookingRepairRepository repairs, EscrowReleaseService escrow,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _repairs = repairs;
        _escrow = escrow;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingRepairsDto> HandleAsync(Guid customerId, Guid bookingId, DecideRepairsRequest request, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("This booking was not found.");
        var all = await _repairs.ListForBookingAsync(booking.Id, ct);
        var pending = all.Where(r => r.Status == BookingRepairStatus.Proposed).ToList();
        if (pending.Count == 0) throw new ConflictException("There are no repairs waiting for your answer.");

        var approvedIds = (request.ApprovedRepairIds ?? new()).ToHashSet();
        if (approvedIds.Any(id => pending.All(r => r.Id != id)))
            throw new ArgumentException("One of those repairs is not waiting for your answer.");

        var now = _clock.UtcNow;
        var approvedNaira = 0;
        foreach (var r in pending)
        {
            var approve = approvedIds.Contains(r.Id);
            r.Decide(approve, now);
            if (approve) approvedNaira += r.TotalNaira;
        }

        if (approvedNaira > 0)
        {
            booking.AddApprovedRepairs(approvedNaira);
        }
        else if (booking.IsDiagnosis && booking.ApprovedRepairsNaira == 0 && booking.Status == BookingStatus.Arrived)
        {
            // Nothing approved after a diagnosis: the visit was the job. The artisan earns the fee.
            booking.CompleteAsDiagnosisOnly(now);
            await _escrow.ReleaseIfPaidAsync(booking, ct);
        }

        await _notifications.RepairsDecidedAsync(booking, approvedNaira, ct);
        await _bookings.SaveChangesAsync(ct);
        return booking.ToRepairsDto(all);
    }
}
