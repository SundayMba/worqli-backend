namespace Servika.Domain.Catalogue;

/// <summary>
/// An artisan's public marketplace profile — what a customer sees when browsing
/// nearby artisans and on the artisan detail screen. In a later slice this will
/// link to a <c>User</c> account and carry KYC/verification state; for the
/// catalogue slice it is read-only reference data the customer app consumes.
/// </summary>
public sealed class ArtisanProfile
{
    public Guid Id { get; private set; }

    /// <summary>The artisan's login account, once linked. Null for catalogue
    /// reference profiles that don't yet have a <c>User</c> behind them. This is
    /// what lets a signed-in artisan be matched to the jobs assigned to them.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Stable key the client uses to resolve the bundled avatar/cover
    /// artwork (e.g. "emeka-okafor"). Decouples images from the database id.</summary>
    public string ImageKey { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Headline specialty, e.g. "Electrical Specialist".</summary>
    public string Specialty { get; private set; } = string.Empty;

    /// <summary>Average rating out of 5, e.g. 4.8.</summary>
    public double Rating { get; private set; }

    public int ReviewCount { get; private set; }

    /// <summary>Distance from the customer, in kilometres. Used as a fallback
    /// baseline when the request carries no location; otherwise the API computes
    /// the real distance from the customer's coordinates to
    /// <see cref="Latitude"/>/<see cref="Longitude"/>.</summary>
    public double DistanceKm { get; private set; }

    /// <summary>The artisan's base latitude, or null if unknown. Used to compute
    /// real proximity for the "Nearby Artisans" list.</summary>
    public double? Latitude { get; private set; }

    /// <summary>The artisan's base longitude, or null if unknown.</summary>
    public double? Longitude { get; private set; }

    public bool IsAvailable { get; private set; }

    /// <summary>KYC/verification state. Only Verified profiles show in the catalogue.</summary>
    public ArtisanVerificationStatus VerificationStatus { get; private set; }

    /// <summary>Accent colour for the avatar ring, hex e.g. "#F97316".</summary>
    public string Accent { get; private set; } = string.Empty;

    public int ExperienceYears { get; private set; }

    /// <summary>City / region, e.g. "Lagos, Nigeria".</summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>Typical response time, e.g. "15 min".</summary>
    public string ResponseTime { get; private set; } = string.Empty;

    /// <summary>Jobs-completed badge text, e.g. "120+".</summary>
    public string JobsCount { get; private set; } = string.Empty;

    /// <summary>Up-front inspection fee in Naira. Formatted for display on the client.</summary>
    public int InspectionFeeNaira { get; private set; }

    public string About { get; private set; } = string.Empty;

    /// <summary>Category slugs this artisan serves (used to list by category).</summary>
    public List<string> CategorySlugs { get; private set; } = new();

    /// <summary>Service chips shown on the profile, e.g. ["Installation","Repair"].</summary>
    public List<string> Services { get; private set; } = new();

    /// <summary>Work-gallery image keys the client resolves to bundled photos.</summary>
    public List<string> GalleryKeys { get; private set; } = new();

    private ArtisanProfile() { }

    public static ArtisanProfile Create(
        Guid id,
        string imageKey,
        string fullName,
        string specialty,
        double rating,
        int reviewCount,
        double distanceKm,
        bool isAvailable,
        string accent,
        int experienceYears,
        string location,
        string responseTime,
        string jobsCount,
        int inspectionFeeNaira,
        string about,
        List<string> categorySlugs,
        List<string> services,
        List<string> galleryKeys,
        Guid? userId = null,
        double? latitude = null,
        double? longitude = null) =>
        new()
        {
            Id = id,
            UserId = userId,
            VerificationStatus = ArtisanVerificationStatus.Verified,
            Latitude = latitude,
            Longitude = longitude,
            ImageKey = imageKey,
            FullName = fullName,
            Specialty = specialty,
            Rating = rating,
            ReviewCount = reviewCount,
            DistanceKm = distanceKm,
            IsAvailable = isAvailable,
            Accent = accent,
            ExperienceYears = experienceYears,
            Location = location,
            ResponseTime = responseTime,
            JobsCount = jobsCount,
            InspectionFeeNaira = inspectionFeeNaira,
            About = about,
            CategorySlugs = categorySlugs,
            Services = services,
            GalleryKeys = galleryKeys,
        };

    /// <summary>
    /// Creates a marketplace profile for a self-onboarding artisan (linked to their
    /// <see cref="User"/> account). Starts with zeroed reputation (no rating/reviews
    /// yet) and, for now, auto-<see cref="ArtisanVerificationStatus.Verified"/> so the
    /// artisan is immediately bookable — admin KYC review replaces the auto-approve
    /// in a later slice. Reputation, distance, etc. accrue as they work.
    /// </summary>
    public static ArtisanProfile CreateForUser(
        Guid userId,
        string fullName,
        string specialty,
        List<string> categorySlugs,
        List<string> services,
        string about,
        int experienceYears,
        string location,
        int inspectionFeeNaira,
        double? latitude,
        double? longitude,
        string imageKey)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(specialty))
            throw new ArgumentException("Specialty is required.", nameof(specialty));
        if (categorySlugs is null || categorySlugs.Count == 0)
            throw new ArgumentException("At least one service category is required.", nameof(categorySlugs));
        if (inspectionFeeNaira < 0)
            throw new ArgumentException("Inspection fee cannot be negative.", nameof(inspectionFeeNaira));

        return new ArtisanProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            // Starts Pending — hidden from the catalogue until KYC is approved.
            VerificationStatus = ArtisanVerificationStatus.Pending,
            ImageKey = imageKey?.Trim() ?? string.Empty,
            FullName = fullName.Trim(),
            Specialty = specialty.Trim(),
            Rating = 0,
            ReviewCount = 0,
            DistanceKm = 0,
            IsAvailable = true,
            Accent = "#F97316",
            ExperienceYears = experienceYears < 0 ? 0 : experienceYears,
            Location = location?.Trim() ?? string.Empty,
            ResponseTime = "New",
            JobsCount = "0",
            InspectionFeeNaira = inspectionFeeNaira,
            About = about?.Trim() ?? string.Empty,
            CategorySlugs = categorySlugs,
            Services = services ?? new(),
            GalleryKeys = new(),
            Latitude = latitude,
            Longitude = longitude,
        };
    }

    /// <summary>Updates the editable parts of a self-managed artisan profile.</summary>
    public void UpdateDetails(
        string specialty,
        List<string> categorySlugs,
        List<string> services,
        string about,
        int experienceYears,
        string location,
        int inspectionFeeNaira,
        double? latitude,
        double? longitude)
    {
        if (string.IsNullOrWhiteSpace(specialty))
            throw new ArgumentException("Specialty is required.", nameof(specialty));
        if (categorySlugs is null || categorySlugs.Count == 0)
            throw new ArgumentException("At least one service category is required.", nameof(categorySlugs));
        if (inspectionFeeNaira < 0)
            throw new ArgumentException("Inspection fee cannot be negative.", nameof(inspectionFeeNaira));

        Specialty = specialty.Trim();
        CategorySlugs = categorySlugs;
        Services = services ?? new();
        About = about?.Trim() ?? string.Empty;
        ExperienceYears = experienceYears < 0 ? 0 : experienceYears;
        Location = location?.Trim() ?? string.Empty;
        InspectionFeeNaira = inspectionFeeNaira;
        if (latitude.HasValue) Latitude = latitude;
        if (longitude.HasValue) Longitude = longitude;
    }

    /// <summary>Toggles the artisan's availability (online/offline).</summary>
    public void SetAvailability(bool available) => IsAvailable = available;

    /// <summary>KYC approved → profile becomes visible/bookable in the catalogue.</summary>
    public void MarkVerified() => VerificationStatus = ArtisanVerificationStatus.Verified;

    /// <summary>KYC rejected → profile stays hidden until re-submitted and approved.</summary>
    public void MarkRejected() => VerificationStatus = ArtisanVerificationStatus.Rejected;

    /// <summary>
    /// Folds a new star rating into the running average and bumps the review
    /// count. <see cref="Rating"/> is treated as the average of
    /// <see cref="ReviewCount"/> ratings, so the update is exact and needs no
    /// full re-scan of the reviews table. The seeded rating/count act as the
    /// launch baseline that real reviews accumulate on top of.
    /// </summary>
    public void AddRating(int stars)
    {
        if (stars is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(stars));

        var total = Rating * ReviewCount + stars;
        ReviewCount += 1;
        Rating = Math.Round(total / ReviewCount, 2);
    }
}
