namespace Servika.Contracts.Catalogue;

/// <summary>
/// A service category as the mobile app consumes it (GET /api/v1/categories).
/// The client resolves the tile artwork locally from <see cref="Slug"/>, so no
/// image URL is sent.
/// </summary>
/// <param name="Id">Stable database id.</param>
/// <param name="Slug">Lookup key, e.g. "electrical".</param>
/// <param name="Name">Display name, e.g. "AC Repair".</param>
/// <param name="Tint">Hex tint for the tile wash, e.g. "#F59E0B".</param>
/// <param name="IconKey">Vector-icon key for categories with no tile image.</param>
/// <param name="SortOrder">Ascending display order.</param>
/// <param name="IsPopular">True if shown in the home "Popular Services" grid.</param>
public sealed record CategoryDto(
    Guid Id,
    string Slug,
    string Name,
    string Tint,
    string? IconKey,
    int SortOrder,
    bool IsPopular);
