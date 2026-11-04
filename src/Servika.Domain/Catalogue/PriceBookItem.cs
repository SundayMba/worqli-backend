namespace Servika.Domain.Catalogue;

/// <summary>What kind of line a price-book item is (Business Model v1.4, section 7).</summary>
public enum PriceBookItemKind
{
    /// <summary>A whole job with a defined scope and one Servika price, paid upfront ("AC servicing").</summary>
    Fixed = 0,

    /// <summary>A repair the artisan may add after a diagnosis visit, at Servika's price ("Replace capacitor").</summary>
    AddOn = 1,

    /// <summary>Work outside the list: no price, only the typical labour range a custom quote is checked against.</summary>
    Custom = 2,
}

/// <summary>
/// One line of Servika's price book. Servika sets every price here, never the artisan: fixed jobs are
/// booked and paid at this price, add-ons are the only repairs an artisan can pick after a diagnosis,
/// and custom items carry the typical range a quote is shown beside (and reviewed against). Prices are
/// labour; parts are charged separately unless the scope says otherwise.
/// </summary>
public sealed class PriceBookItem
{
    public const int MaxNameLength = 80;
    public const int MaxScopeLength = 240;
    public const int MaxPartsNoteLength = 160;
    /// <summary>No single price-book line may exceed this; bigger work is a custom quote.</summary>
    public const int MaxPriceNaira = 5_000_000;

    public Guid Id { get; private set; }
    public string CategorySlug { get; private set; } = string.Empty;
    public PriceBookItemKind Kind { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>What the price covers, in one line ("1–1.5HP split unit: wash coils and filters, check gas").</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>The Servika price (Fixed and AddOn). Null for a Custom range item.</summary>
    public int? PriceNaira { get; private set; }

    /// <summary>The typical labour range (Custom items; optional context on the others).</summary>
    public int? RangeMinNaira { get; private set; }
    public int? RangeMaxNaira { get; private set; }

    /// <summary>How parts are charged ("Oil and plug at listed parts price"), if at all.</summary>
    public string? PartsNote { get; private set; }

    /// <summary>Roughly how long the job takes, for the customer's plans.</summary>
    public int? DurationMinutes { get; private set; }

    /// <summary>The admin-uploaded cover photo (storage key). Null → the app shows its placeholder.</summary>
    public string? PhotoKey { get; private set; }

    /// <summary>Shown in the "Fixed prices" rail on Home.</summary>
    public bool IsPopular { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>True while the price is a placeholder, not yet set from real phone quotes. The console
    /// flags it so nothing goes live by accident on a guessed number.</summary>
    public bool IsPlaceholderPrice { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private PriceBookItem() { }

    public static PriceBookItem Create(
        string categorySlug, PriceBookItemKind kind, string name, string scope,
        int? priceNaira, int? rangeMinNaira, int? rangeMaxNaira, string? partsNote, int? durationMinutes,
        bool isPopular, bool isActive, int sortOrder, bool isPlaceholderPrice, Guid? byUserId, DateTimeOffset now)
    {
        var item = new PriceBookItem { Id = Guid.NewGuid(), CreatedAtUtc = now };
        item.Apply(categorySlug, kind, name, scope, priceNaira, rangeMinNaira, rangeMaxNaira, partsNote,
            durationMinutes, isPopular, isActive, sortOrder, isPlaceholderPrice, byUserId, now);
        return item;
    }

    public void Update(
        string categorySlug, PriceBookItemKind kind, string name, string scope,
        int? priceNaira, int? rangeMinNaira, int? rangeMaxNaira, string? partsNote, int? durationMinutes,
        bool isPopular, bool isActive, int sortOrder, bool isPlaceholderPrice, Guid? byUserId, DateTimeOffset now) =>
        Apply(categorySlug, kind, name, scope, priceNaira, rangeMinNaira, rangeMaxNaira, partsNote,
            durationMinutes, isPopular, isActive, sortOrder, isPlaceholderPrice, byUserId, now);

    public void SetPhoto(string? photoKey, Guid? byUserId, DateTimeOffset now)
    {
        PhotoKey = string.IsNullOrWhiteSpace(photoKey) ? null : photoKey;
        UpdatedByUserId = byUserId;
        UpdatedAtUtc = now;
    }

    /// <summary>Whether a custom quote's labour is far enough above the typical range that ops must see it
    /// before the customer does (Business Model v1.4: above 1.5× the range).</summary>
    public bool IsAboveRange(int labourNaira, decimal factor) =>
        RangeMaxNaira is { } max && labourNaira > max * factor;

    private void Apply(
        string categorySlug, PriceBookItemKind kind, string name, string scope,
        int? priceNaira, int? rangeMinNaira, int? rangeMaxNaira, string? partsNote, int? durationMinutes,
        bool isPopular, bool isActive, int sortOrder, bool isPlaceholderPrice, Guid? byUserId, DateTimeOffset now)
    {
        var slug = categorySlug?.Trim().ToLowerInvariant() ?? string.Empty;
        var cleanName = name?.Trim() ?? string.Empty;
        var cleanScope = scope?.Trim() ?? string.Empty;
        var cleanParts = string.IsNullOrWhiteSpace(partsNote) ? null : partsNote.Trim();

        if (slug.Length == 0) throw new ArgumentException("Pick a category.");
        if (cleanName.Length == 0) throw new ArgumentException("Give the item a name.");
        if (cleanName.Length > MaxNameLength) throw new ArgumentException($"Keep the name under {MaxNameLength} characters.");
        if (cleanScope.Length > MaxScopeLength) throw new ArgumentException($"Keep what it covers under {MaxScopeLength} characters.");
        if (cleanParts is { Length: > MaxPartsNoteLength }) throw new ArgumentException($"Keep the parts note under {MaxPartsNoteLength} characters.");
        if (durationMinutes is <= 0 or > 24 * 60) throw new ArgumentException("Duration must be between 1 minute and a day.");

        if (kind is PriceBookItemKind.Fixed or PriceBookItemKind.AddOn)
        {
            if (priceNaira is not { } price || price <= 0)
                throw new ArgumentException("Fixed jobs and add-ons need a price.");
            if (price > MaxPriceNaira) throw new ArgumentException($"A single item can't cost more than ₦{MaxPriceNaira:N0}; make it a custom quote.");
        }
        else
        {
            priceNaira = null;
            if (rangeMinNaira is null || rangeMaxNaira is null)
                throw new ArgumentException("A custom item needs its typical range (lowest and highest labour).");
        }

        if (rangeMinNaira is < 0 || rangeMaxNaira is < 0) throw new ArgumentException("The range can't be negative.");
        if (rangeMinNaira is { } lo && rangeMaxNaira is { } hi && lo > hi)
            throw new ArgumentException("The lowest price must not be above the highest.");
        if (rangeMaxNaira > MaxPriceNaira) throw new ArgumentException($"Keep the range under ₦{MaxPriceNaira:N0}.");

        // Only whole jobs are sold on Home; an add-on or custom line can never be "popular".
        if (kind != PriceBookItemKind.Fixed) isPopular = false;

        CategorySlug = slug;
        Kind = kind;
        Name = cleanName;
        Scope = cleanScope;
        PriceNaira = priceNaira;
        RangeMinNaira = rangeMinNaira;
        RangeMaxNaira = rangeMaxNaira;
        PartsNote = cleanParts;
        DurationMinutes = durationMinutes;
        IsPopular = isPopular;
        IsActive = isActive;
        SortOrder = sortOrder;
        IsPlaceholderPrice = isPlaceholderPrice;
        UpdatedByUserId = byUserId;
        UpdatedAtUtc = now;
    }
}
