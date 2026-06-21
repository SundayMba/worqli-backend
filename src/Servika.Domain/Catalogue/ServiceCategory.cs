namespace Servika.Domain.Catalogue;

/// <summary>
/// A platform-managed service category shown in the marketplace (Electrical,
/// Plumbing, AC Repair, …). Reference data: the catalogue team curates the list,
/// customers only read it. Like <c>User</c>, this lives in the Domain and knows
/// nothing about the database or the web.
/// </summary>
public sealed class ServiceCategory
{
    public Guid Id { get; private set; }

    /// <summary>URL/lookup key, e.g. "electrical". Stable; used in routes and to
    /// resolve the bundled tile artwork on the client.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Display name, e.g. "AC Repair".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Brand tint for the tile wash, as a hex string e.g. "#F59E0B".</summary>
    public string Tint { get; private set; } = string.Empty;

    /// <summary>Optional vector-icon key used when there is no tile image.</summary>
    public string? IconKey { get; private set; }

    /// <summary>Display order in the grid (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Whether the category appears in the home "Popular Services" grid.</summary>
    public bool IsPopular { get; private set; }

    /// <summary>Soft on/off switch so a category can be hidden without deletion.</summary>
    public bool IsActive { get; private set; }

    private ServiceCategory() { }

    public static ServiceCategory Create(
        Guid id,
        string slug,
        string name,
        string tint,
        int sortOrder,
        bool isPopular = false,
        string? iconKey = null,
        bool isActive = true) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = name,
            Tint = tint,
            SortOrder = sortOrder,
            IsPopular = isPopular,
            IconKey = iconKey,
            IsActive = isActive,
        };
}
