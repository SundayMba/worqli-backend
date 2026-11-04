namespace Servika.Domain.Catalogue;

/// <summary>Where a service post is in ops' check.</summary>
public enum ServicePostReview
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

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

    // ── Pro redesign (2026-09): the listing as the customer reads it ──

    /// <summary>Paused listings stay visible to the artisan only (design 48).</summary>
    public bool IsActive { get; private set; } = true;
    /// <summary>"How long", e.g. 180 for about 3 hours; null = not stated.</summary>
    public int? DurationMinutes { get; private set; }
    /// <summary>"What it includes" lines, in the artisan's own words (max 6).</summary>
    public List<string> Includes { get; private set; } = new();
    /// <summary>Optional free text shown on the customer's service page.</summary>
    public string? Description { get; private set; }

    public void SetActive(bool active) => IsActive = active;

    // ── Ops review (user decision 2026-11-04) ──
    // Every post is checked before customers see it, cover photo included: it must show the artisan at work or the
    // work itself, never a phone number, office or shop address. Any change sends it back for a check.
    /// <summary>The trade this post sits under on Home ("Electrical standard services").</summary>
    public string CategorySlug { get; private set; } = string.Empty;
    public ServicePostReview Review { get; private set; } = ServicePostReview.Pending;
    /// <summary>Why ops turned the post down, shown to the artisan.</summary>
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public const int MaxReviewNoteLength = 400;

    /// <summary>Customers see a post only when it is live and ops passed it.</summary>
    public bool IsPublic => IsActive && Review == ServicePostReview.Approved;

    public void SetCategory(string categorySlug)
    {
        var slug = categorySlug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (slug.Length == 0) throw new ArgumentException("Pick the trade this service belongs to.");
        CategorySlug = slug;
    }

    /// <summary>Something the customer sees changed: back to ops for a check.</summary>
    public void SubmitForReview(DateTimeOffset now)
    {
        Review = ServicePostReview.Pending;
        ReviewNote = null;
        SubmittedAtUtc = now;
        ReviewedAtUtc = null;
        ReviewedByUserId = null;
    }

    public void Approve(Guid adminUserId, DateTimeOffset now)
    {
        Review = ServicePostReview.Approved;
        ReviewNote = null;
        ReviewedAtUtc = now;
        ReviewedByUserId = adminUserId;
    }

    public void Reject(Guid adminUserId, string note, DateTimeOffset now)
    {
        var clean = note?.Trim() ?? string.Empty;
        if (clean.Length < 10) throw new ArgumentException("Tell the artisan what to change (at least a short sentence).");
        if (clean.Length > MaxReviewNoteLength) throw new ArgumentException($"Keep the note under {MaxReviewNoteLength} characters.");
        Review = ServicePostReview.Rejected;
        ReviewNote = clean;
        ReviewedAtUtc = now;
        ReviewedByUserId = adminUserId;
    }

    public void UpdateDetails(int? durationMinutes, IEnumerable<string>? includes, string? description)
    {
        DurationMinutes = durationMinutes is > 0 ? Math.Min(durationMinutes.Value, 24 * 60 * 7) : null;
        Includes = (includes ?? Enumerable.Empty<string>())
            .Select(i => (i ?? string.Empty).Trim())
            .Where(i => i.Length > 0)
            .Select(i => i.Length > 80 ? i[..80] : i)
            .Take(6)
            .ToList();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()[..Math.Min(description.Trim().Length, 600)];
    }

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
