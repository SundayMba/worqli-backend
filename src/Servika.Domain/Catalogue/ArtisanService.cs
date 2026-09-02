namespace Servika.Domain.Catalogue;

/// <summary>
/// A fixed-price service an artisan publishes on their profile — "Knotless
/// braids — ₦15,000". The price is known before booking, so a customer books
/// it directly at that price (no quote round-trip) and pays the moment the
/// artisan accepts. Quote-based work (repairs) never uses these; it goes
/// through the bid/quote flow instead.
/// </summary>
public sealed class ArtisanService
{
    public Guid Id { get; private set; }

    /// <summary>The publishing artisan's marketplace profile.</summary>
    public Guid ArtisanProfileId { get; private set; }

    /// <summary>What the customer books, e.g. "Knotless braids (medium)".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The published all-in price for this service.</summary>
    public int PriceNaira { get; private set; }

    /// <summary>Storage key of the service's showcase photo (the work itself —
    /// braids done, an installed unit), or null. Drives the Home discovery rail.</summary>
    public string? PhotoKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public const int MaxNameLength = 80;

    private ArtisanService() { }

    public static ArtisanService Create(
        Guid artisanProfileId, string name, int priceNaira, DateTimeOffset now)
    {
        if (artisanProfileId == Guid.Empty)
            throw new ArgumentException("Artisan profile is required.", nameof(artisanProfileId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A service name is required.", nameof(name));
        if (priceNaira <= 0)
            throw new ArgumentException("Set a price greater than zero.", nameof(priceNaira));

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength) trimmed = trimmed[..MaxNameLength];

        return new ArtisanService
        {
            Id = Guid.NewGuid(),
            ArtisanProfileId = artisanProfileId,
            Name = trimmed,
            PriceNaira = priceNaira,
            CreatedAt = now,
        };
    }

    public void SetPhoto(string photoKey)
    {
        if (string.IsNullOrWhiteSpace(photoKey))
            throw new ArgumentException("A photo key is required.", nameof(photoKey));
        PhotoKey = photoKey;
    }

    /// <summary>Updates the published price (re-adding the same name revises it).</summary>
    public void Reprice(int priceNaira)
    {
        if (priceNaira <= 0)
            throw new ArgumentException("Set a price greater than zero.", nameof(priceNaira));
        PriceNaira = priceNaira;
    }
}
