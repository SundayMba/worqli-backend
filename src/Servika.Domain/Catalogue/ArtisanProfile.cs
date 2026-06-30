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

    /// <summary>Distance from the customer, in kilometres. Static seed value for
    /// now; computed from live location in a later slice.</summary>
    public double DistanceKm { get; private set; }

    public bool IsAvailable { get; private set; }

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
        Guid? userId = null) =>
        new()
        {
            Id = id,
            UserId = userId,
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
}
