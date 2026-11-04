namespace Servika.Domain.Catalogue;

/// <summary>
/// "Tell us what you need" (Business Model v1.4, section 15): a customer asked for a service Servika does
/// not offer yet, or from an area it does not cover. Measures demand before supply is committed.
/// </summary>
public sealed class ServiceInterest
{
    public const int MaxNoteLength = 300;

    public Guid Id { get; private set; }
    /// <summary>The category asked about (a "coming soon" one), or null for free text only.</summary>
    public string? CategorySlug { get; private set; }
    public string? Note { get; private set; }
    public string? AreaLabel { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    /// <summary>The signed-in customer, or null for a guest.</summary>
    public Guid? UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ServiceInterest() { }

    public static ServiceInterest Record(string? categorySlug, string? note, string? areaLabel, double? lat, double? lng, Guid? userId, DateTimeOffset now)
    {
        var slug = string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim().ToLowerInvariant();
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (slug is null && text is null) throw new ArgumentException("Say what you need.");
        if (text is { Length: > MaxNoteLength }) throw new ArgumentException($"Keep it under {MaxNoteLength} characters.");
        if (lat is < -90 or > 90 || lng is < -180 or > 180) throw new ArgumentException("That location is not valid.");
        return new ServiceInterest
        {
            Id = Guid.NewGuid(),
            CategorySlug = slug,
            Note = text,
            AreaLabel = string.IsNullOrWhiteSpace(areaLabel) ? null : areaLabel.Trim()[..Math.Min(areaLabel.Trim().Length, 120)],
            Latitude = lat,
            Longitude = lng,
            UserId = userId,
            CreatedAtUtc = now,
        };
    }
}
