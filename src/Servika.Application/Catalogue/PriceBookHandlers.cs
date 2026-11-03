using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Contracts.Catalogue;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Maps price-book items to their DTOs.</summary>
public static class PriceBookMapping
{
    /// <summary>The cover photo URL, versioned by the last edit so a new upload is never served from cache.</summary>
    public static string? PhotoUrl(PriceBookItem item) =>
        item.PhotoKey is null ? null : $"/api/v1/price-book/{item.Id}/photo?v={item.UpdatedAtUtc.ToUnixTimeSeconds()}";

    public static PriceBookItemDto ToDto(this PriceBookItem item, string categoryName) =>
        new(item.Id, item.CategorySlug, categoryName, item.Kind.ToString(), item.Name, item.Scope,
            item.PriceNaira, item.RangeMinNaira, item.RangeMaxNaira, item.PartsNote, item.DurationMinutes,
            PhotoUrl(item), item.IsPopular);

    public static PriceBookItemKind ParseKind(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "fixed" => PriceBookItemKind.Fixed,
            "addon" or "add-on" => PriceBookItemKind.AddOn,
            "custom" => PriceBookItemKind.Custom,
            _ => throw new ArgumentException("Kind must be 'Fixed', 'AddOn' or 'Custom'."),
        };
}

/// <summary>
/// The public price book: active items in categories customers can book. Filters by category, kind and
/// "popular" (the Home rail). Nothing from a hidden category is ever shown.
/// </summary>
public sealed class GetPriceBookHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;

    public GetPriceBookHandler(IPriceBookRepository priceBook, ICatalogueRepository catalogue)
    {
        _priceBook = priceBook;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<PriceBookItemDto>> HandleAsync(
        string? categorySlug, string? kind, bool popularOnly, CancellationToken ct)
    {
        var slug = string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim().ToLowerInvariant();
        var parsedKind = string.IsNullOrWhiteSpace(kind) ? (PriceBookItemKind?)null : PriceBookMapping.ParseKind(kind);
        var categories = (await _catalogue.GetCategoriesAsync(ct)).ToDictionary(c => c.Slug, c => c.Name);
        var items = await _priceBook.ListActiveAsync(slug, popularOnly ? PriceBookItemKind.Fixed : parsedKind, ct);
        return items
            .Where(i => categories.ContainsKey(i.CategorySlug) && (!popularOnly || i.IsPopular))
            .Select(i => i.ToDto(categories[i.CategorySlug]))
            .ToList();
    }
}

/// <summary>A category screen: its fixed jobs, the diagnosis fee and how many repairs a diagnosis covers.</summary>
public sealed class GetCategoryPriceBookHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;
    private readonly IPlatformSettingsRepository _settings;

    public GetCategoryPriceBookHandler(
        IPriceBookRepository priceBook, ICatalogueRepository catalogue, IPlatformSettingsRepository settings)
    {
        _priceBook = priceBook;
        _catalogue = catalogue;
        _settings = settings;
    }

    public async Task<CategoryPriceBookDto> HandleAsync(string slug, CancellationToken ct)
    {
        var clean = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        var category = await _catalogue.GetCategoryBySlugAsync(clean, ct);
        if (category is not { IsActive: true }) throw new NotFoundException("That service is not offered.");

        var items = await _priceBook.ListActiveAsync(clean, null, ct);
        var settings = await _settings.GetOrCreateAsync(ct);
        return new CategoryPriceBookDto(
            category.Slug,
            category.Name,
            items.Where(i => i.Kind == PriceBookItemKind.Fixed).Select(i => i.ToDto(category.Name)).ToList(),
            settings.DiagnosisFeeNaira,
            items.Count(i => i.Kind == PriceBookItemKind.AddOn));
    }
}

/// <summary>One active item (the job screen before booking). 404 when hidden or retired.</summary>
public sealed class GetPriceBookItemHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;

    public GetPriceBookItemHandler(IPriceBookRepository priceBook, ICatalogueRepository catalogue)
    {
        _priceBook = priceBook;
        _catalogue = catalogue;
    }

    public async Task<PriceBookItemDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await _priceBook.FindAsync(id, ct);
        var category = item is null ? null : await _catalogue.GetCategoryBySlugAsync(item.CategorySlug, ct);
        if (item is not { IsActive: true } || category is not { IsActive: true })
            throw new NotFoundException("That job is no longer offered.");
        return item.ToDto(category.Name);
    }
}

/// <summary>Serves a price-book cover photo. 404 when there is none (the app shows its placeholder).</summary>
public sealed class GetPriceBookPhotoHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly IFileStorage _files;

    public GetPriceBookPhotoHandler(IPriceBookRepository priceBook, IFileStorage files)
    {
        _priceBook = priceBook;
        _files = files;
    }

    public async Task<StoredFile> HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await _priceBook.FindAsync(id, ct);
        if (item?.PhotoKey is not { } key) throw new NotFoundException("This job has no photo.");
        return await _files.GetAsync(key, ct) ?? throw new NotFoundException("The photo file was not found.");
    }
}

/// <summary>
/// Reads the market's answer to each price from the last 30 days of real jobs. Pure, so the rules are
/// unit-tested. Artisans passing (above all "Price too low") or rotation running out says the price is
/// too low; customers stopping at the payment step or cancelling says it is too high. Fewer than
/// <see cref="MinBookings"/> bookings is not enough to judge.
/// </summary>
public static class PriceSignals
{
    public const int WindowDays = 30;
    public const int MinBookings = 5;
    public const string PriceTooLowReason = "Price too low";

    public static PriceSignalsDto Compute(
        IReadOnlyList<PriceBookBookingFact> bookings, IReadOnlyList<PriceBookOfferFact> offers)
    {
        var booked = bookings.Count;
        var paid = bookings.Count(b => b.PaymentState is BookingPaymentState.Paid
            or BookingPaymentState.Refunded or BookingPaymentState.PartiallyRefunded);
        var completed = bookings.Count(b => b.Status == BookingStatus.Completed);
        var leftUnpaid = bookings.Count(b => b.Status == BookingStatus.Expired
            || b.Status == BookingStatus.Cancelled && b.PaymentState is BookingPaymentState.Unpaid or BookingPaymentState.Pending);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var ranOut = bookings.Count(b => b.OffersExhaustedAtUtc is not null);

        var made = offers.Count;
        var accepted = offers.Count(o => o.Status == JobOfferStatus.Accepted);
        var passed = offers.Count(o => o.Status == JobOfferStatus.Declined);
        var timedOut = offers.Count(o => o.Status == JobOfferStatus.Expired);
        var reasons = offers
            .Where(o => o.Status == JobOfferStatus.Declined && !string.IsNullOrWhiteSpace(o.DeclineReason))
            .GroupBy(o => o.DeclineReason!.Trim())
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();
        var priceTooLow = offers.Count(o => o.Status == JobOfferStatus.Declined
            && string.Equals(o.DeclineReason?.Trim(), PriceTooLowReason, StringComparison.OrdinalIgnoreCase));

        var (verdict, note) = Judge(booked, paid, leftUnpaid, cancelled, ranOut, made, passed, timedOut, priceTooLow);
        return new PriceSignalsDto(booked, paid, completed, leftUnpaid, cancelled, made, accepted, passed, timedOut,
            ranOut, reasons, verdict, note);
    }

    private static (string, string) Judge(
        int booked, int paid, int leftUnpaid, int cancelled, int ranOut, int made, int passed, int timedOut, int priceTooLow)
    {
        if (booked < MinBookings)
            return ("TooFewJobs", $"Fewer than {MinBookings} bookings in {WindowDays} days. Too early to judge the price.");

        var notTaken = made == 0 ? 0 : (double)(passed + timedOut) / made;
        var tooLow = made >= MinBookings && (notTaken >= 0.5 || (double)priceTooLow / made >= 0.2)
                     || paid > 0 && (double)ranOut / paid >= 0.25;
        var tooHigh = (double)leftUnpaid / booked >= 0.4 || (double)cancelled / booked >= 0.3;

        if (tooLow && tooHigh)
            return ("Mixed", "Artisans are passing and customers are dropping out. Check the scope before moving the price.");
        if (tooLow)
            return ("MaybeTooLow", $"Artisans did not take {Pct(notTaken)} of offers. The price may be too low for the work.");
        if (tooHigh)
            return ("MaybeTooHigh", $"{Pct((double)leftUnpaid / booked)} of customers stopped at the payment step. The price may be too high.");
        return ("LooksRight", "Artisans take it and customers pay. Leave it.");
    }

    private static string Pct(double fraction) => $"{Math.Round(fraction * 100):0}%";
}

/// <summary>The console's price book: every item with its price signals and who last changed it.</summary>
public sealed class ListAdminPriceBookHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public ListAdminPriceBookHandler(
        IPriceBookRepository priceBook, ICatalogueRepository catalogue, IUserRepository users, IClock clock)
    {
        _priceBook = priceBook;
        _catalogue = catalogue;
        _users = users;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AdminPriceBookItemDto>> HandleAsync(CancellationToken ct)
    {
        var items = await _priceBook.ListAllAsync(ct);
        var categories = (await _catalogue.GetAllCategoriesAsync(ct)).ToDictionary(c => c.Slug, c => c.Name);
        var since = _clock.UtcNow.AddDays(-PriceSignals.WindowDays);
        var bookings = (await _priceBook.ListBookingFactsSinceAsync(since, ct)).ToLookup(b => b.ItemId);
        var offers = (await _priceBook.ListOfferFactsSinceAsync(since, ct)).ToLookup(o => o.ItemId);

        var names = new Dictionary<Guid, string>();
        foreach (var id in items.Select(i => i.UpdatedByUserId).OfType<Guid>().Distinct())
            if (await _users.FindByIdAsync(id, ct) is { } u) names[id] = u.FullName;

        return items.Select(i => ToAdminDto(i,
                categories.GetValueOrDefault(i.CategorySlug, i.CategorySlug),
                i.UpdatedByUserId is { } by ? names.GetValueOrDefault(by) : null,
                PriceSignals.Compute(bookings[i.Id].ToList(), offers[i.Id].ToList())))
            .ToList();
    }

    public static AdminPriceBookItemDto ToAdminDto(PriceBookItem i, string categoryName, string? updatedBy, PriceSignalsDto signals) =>
        new(i.Id, i.CategorySlug, categoryName, i.Kind.ToString(), i.Name, i.Scope, i.PriceNaira,
            i.RangeMinNaira, i.RangeMaxNaira, i.PartsNote, i.DurationMinutes, PriceBookMapping.PhotoUrl(i),
            i.IsPopular, i.IsActive, i.SortOrder, i.IsPlaceholderPrice, i.UpdatedAtUtc, updatedBy, signals);
}

/// <summary>Creates or edits a price-book line (admin). Prices only ever change here.</summary>
public sealed class SavePriceBookItemHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public SavePriceBookItemHandler(
        IPriceBookRepository priceBook, ICatalogueRepository catalogue, IUserRepository users, IClock clock)
    {
        _priceBook = priceBook;
        _catalogue = catalogue;
        _users = users;
        _clock = clock;
    }

    public async Task<AdminPriceBookItemDto> HandleAsync(
        Guid? id, Guid adminUserId, SavePriceBookItemRequest r, CancellationToken ct)
    {
        var slug = r.CategorySlug?.Trim().ToLowerInvariant() ?? string.Empty;
        var category = await _catalogue.GetCategoryBySlugAsync(slug, ct)
            ?? throw new NotFoundException($"Category '{slug}' was not found.");
        var kind = PriceBookMapping.ParseKind(r.Kind);
        var now = _clock.UtcNow;

        PriceBookItem item;
        if (id is { } existingId)
        {
            item = await _priceBook.FindForUpdateAsync(existingId, ct)
                ?? throw new NotFoundException("That price-book item was not found.");
            item.Update(slug, kind, r.Name, r.Scope, r.PriceNaira, r.RangeMinNaira, r.RangeMaxNaira, r.PartsNote,
                r.DurationMinutes, r.IsPopular, r.IsActive, r.SortOrder, r.IsPlaceholderPrice, adminUserId, now);
        }
        else
        {
            item = PriceBookItem.Create(slug, kind, r.Name, r.Scope, r.PriceNaira, r.RangeMinNaira, r.RangeMaxNaira,
                r.PartsNote, r.DurationMinutes, r.IsPopular, r.IsActive, r.SortOrder, r.IsPlaceholderPrice, adminUserId, now);
            _priceBook.Add(item);
        }
        await _priceBook.SaveChangesAsync(ct);

        var by = (await _users.FindByIdAsync(adminUserId, ct))?.FullName;
        return ListAdminPriceBookHandler.ToAdminDto(item, category.Name, by, PriceSignals.Compute([], []));
    }
}

/// <summary>Sets or clears a price-book cover photo (admin). The old file is removed best-effort.</summary>
public sealed class SetPriceBookPhotoHandler
{
    private readonly IPriceBookRepository _priceBook;
    private readonly IFileStorage _files;
    private readonly IClock _clock;

    public SetPriceBookPhotoHandler(IPriceBookRepository priceBook, IFileStorage files, IClock clock)
    {
        _priceBook = priceBook;
        _files = files;
        _clock = clock;
    }

    public async Task<string?> HandleAsync(Guid id, Guid adminUserId, string? photoBase64, CancellationToken ct)
    {
        var item = await _priceBook.FindForUpdateAsync(id, ct)
            ?? throw new NotFoundException("That price-book item was not found.");

        string? key = null;
        if (!string.IsNullOrWhiteSpace(photoBase64))
        {
            var comma = photoBase64.IndexOf(',');
            var payload = photoBase64.StartsWith("data:") && comma >= 0 ? photoBase64[(comma + 1)..] : photoBase64;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException) { throw new ArgumentException("The photo is not valid base64."); }
            if (bytes.Length == 0) throw new ArgumentException("The photo is empty.");
            if (bytes.Length > 6 * 1024 * 1024) throw new ArgumentException("Keep the photo under 6 MB.");
            key = await _files.SaveAsync(bytes, "image/jpeg", ct);
        }

        var old = item.PhotoKey;
        item.SetPhoto(key, adminUserId, _clock.UtcNow);
        await _priceBook.SaveChangesAsync(ct);
        if (old is not null && old != key) await _files.DeleteAsync(old, ct);
        return PriceBookMapping.PhotoUrl(item);
    }
}
