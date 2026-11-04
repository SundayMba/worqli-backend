namespace Servika.Contracts.Catalogue;

/// <summary>One Servika price-book line as customers and artisans see it. <c>Kind</c> is "Fixed", "AddOn"
/// or "Custom". Prices are labour; <c>PartsNote</c> says how parts are charged.</summary>
public sealed record PriceBookItemDto(
    Guid Id,
    string CategorySlug,
    string CategoryName,
    string Kind,
    string Name,
    string Scope,
    int? PriceNaira,
    int? RangeMinNaira,
    int? RangeMaxNaira,
    string? PartsNote,
    int? DurationMinutes,
    /// <summary>Relative URL of the cover photo, or null (the app shows its placeholder).</summary>
    string? PhotoUrl,
    bool IsPopular);

/// <summary>Everything a category screen needs: the fixed jobs, plus the diagnosis visit fee for
/// "Not sure what's wrong?" and the custom route.</summary>
public sealed record CategoryPriceBookDto(
    string CategorySlug,
    string CategoryName,
    IReadOnlyList<PriceBookItemDto> FixedJobs,
    int DiagnosisFeeNaira,
    /// <summary>How many repairs a diagnosis can lead to at Servika's prices in this category.</summary>
    int AddOnCount);
