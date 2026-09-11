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
    /// artwork (e.g. "emeka-okafor"). Decouples images from the database id.
    /// Seed-era mechanism; self-onboarded artisans upload a real photo instead
    /// (see <see cref="PhotoKey"/>).</summary>
    public string ImageKey { get; private set; } = string.Empty;

    /// <summary>Storage key of the artisan's uploaded profile photo (served via
    /// GET /artisans/{id}/photo), or null if they haven't uploaded one. Takes
    /// precedence over <see cref="ImageKey"/> on the clients.</summary>
    public string? PhotoKey { get; private set; }

    /// <summary>Storage key of the artisan's uploaded cover photo — typically a
    /// shot of them at work — served via GET /artisans/{id}/cover. Null when not
    /// uploaded; clients then fall back to the profile photo, then bundled art.</summary>
    public string? CoverPhotoKey { get; private set; }

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

    /// <summary>When the profile was soft-deleted (its owner's account was deleted).
    /// Null = live. A global query filter hides soft-deleted profiles from the
    /// catalogue, explore and search; the row survives until the account is purged.</summary>
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

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

    /// <summary>Work-gallery image keys the client resolves to bundled photos.
    /// Seed-era mechanism; self-onboarded artisans upload real work-evidence
    /// photos instead (see <see cref="GalleryPhotoKeys"/>).</summary>
    public List<string> GalleryKeys { get; private set; } = new();

    /// <summary>Storage keys of the artisan's uploaded work-evidence photos,
    /// newest first — served via GET /artisans/{id}/gallery/{key}. The artisan
    /// manages these from the Pro app (add after a job / delete).</summary>
    public List<string> GalleryPhotoKeys { get; private set; } = new();

    /// <summary>Storage key of an uploaded work certificate (optional — many
    /// excellent artisans have none). Having one boosts the ranking score while
    /// real ratings accumulate; it is never shown to customers as a document.</summary>
    public string? CertificateKey { get; private set; }

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

    // ── Pro redesign (2026-09): payout account, work preferences, away mode ──

    /// <summary>Saved payout account (bank code + NUBAN + account name). Set once
    /// during verification; withdrawals default to it. Null until saved.</summary>
    public string? PayoutBankCode { get; private set; }
    public string? PayoutBankName { get; private set; }
    public string? PayoutAccountNumber { get; private set; }
    public string? PayoutAccountName { get; private set; }
    public bool HasPayoutAccount => !string.IsNullOrEmpty(PayoutAccountNumber);

    /// <summary>How far the artisan will travel for open jobs (km). Informational
    /// at launch; the customer search still uses proximity ranking.</summary>
    public int WorkRadiusKm { get; private set; } = 8;
    /// <summary>Opted in to urgent ("emergency") requests.</summary>
    public bool AcceptsEmergency { get; private set; }
    /// <summary>Working hours as a small JSON blob the apps own (per weekday).</summary>
    public string? WorkingHoursJson { get; private set; }
    /// <summary>Away mode: hidden from search until this instant. Null = not away.</summary>
    public DateTimeOffset? AwayUntilUtc { get; private set; }
    public bool IsAway(DateTimeOffset now) => AwayUntilUtc is { } u && u > now;

    public void SetPayoutAccount(string bankCode, string bankName, string accountNumber, string accountName)
    {
        var digits = new string((accountNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 10)
            throw new ArgumentException("A Nigerian account number has 10 digits.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(bankCode) || string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Pick the bank from the list.", nameof(bankName));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("Enter the account name exactly as the bank has it.", nameof(accountName));
        PayoutBankCode = bankCode.Trim();
        PayoutBankName = bankName.Trim();
        PayoutAccountNumber = digits;
        PayoutAccountName = accountName.Trim();
    }

    public void SetWorkPreferences(int radiusKm, bool acceptsEmergency, string? workingHoursJson)
    {
        WorkRadiusKm = Math.Clamp(radiusKm, 1, 100);
        AcceptsEmergency = acceptsEmergency;
        WorkingHoursJson = string.IsNullOrWhiteSpace(workingHoursJson) ? null : workingHoursJson;
    }

    /// <summary>Away until a date (hidden from search; accepted jobs untouched), or null to come back.</summary>
    public void SetAway(DateTimeOffset? untilUtc) => AwayUntilUtc = untilUtc;

    // ── Guarantor policy ───────────────────────────────────────────────────
    /// <summary>Admin waived the guarantor requirement for this artisan alone.</summary>
    public bool GuarantorsWaived { get; private set; }
    public void SetGuarantorsWaived(bool waived) => GuarantorsWaived = waived;

    // ── NIN lookup (register check the artisan runs from the identity screen) ──
    /// <summary>The NIN that was last checked against the register.</summary>
    public string? NinLookupNumber { get; private set; }
    /// <summary>Matched | NameMismatch | NotFound | Failed, or null when never checked.</summary>
    public string? NinLookupStatus { get; private set; }
    /// <summary>The name the register returned, for the reviewer to compare.</summary>
    public string? NinLookupName { get; private set; }
    public DateTimeOffset? NinLookupCheckedAtUtc { get; private set; }
    /// <summary>Lookups made in the current UTC day (they cost money; three a day).</summary>
    public int NinLookupAttemptsToday { get; private set; }
    public DateTimeOffset? NinLookupAttemptsDayUtc { get; private set; }

    public const int MaxNinLookupsPerDay = 3;

    public int NinLookupsLeft(DateTimeOffset now)
    {
        var sameDay = NinLookupAttemptsDayUtc is { } d && d.UtcDateTime.Date == now.UtcDateTime.Date;
        return Math.Max(0, MaxNinLookupsPerDay - (sameDay ? NinLookupAttemptsToday : 0));
    }

    /// <summary>Records one register lookup and its outcome. Callers check <see cref="NinLookupsLeft"/> first.</summary>
    public void RecordNinLookup(string nin, string status, string? registerName, DateTimeOffset now)
    {
        var sameDay = NinLookupAttemptsDayUtc is { } d && d.UtcDateTime.Date == now.UtcDateTime.Date;
        NinLookupAttemptsToday = sameDay ? NinLookupAttemptsToday + 1 : 1;
        NinLookupAttemptsDayUtc = now;
        NinLookupNumber = nin;
        NinLookupStatus = status;
        NinLookupName = registerName;
        NinLookupCheckedAtUtc = now;
    }

    /// <summary>Points the profile at a newly uploaded photo (storage key).</summary>
    public void SetPhoto(string photoKey)
    {
        if (string.IsNullOrWhiteSpace(photoKey))
            throw new ArgumentException("Photo key is required.", nameof(photoKey));
        PhotoKey = photoKey;
    }

    /// <summary>Points the profile at a newly uploaded cover photo (storage key).</summary>
    public void SetCoverPhoto(string coverPhotoKey)
    {
        if (string.IsNullOrWhiteSpace(coverPhotoKey))
            throw new ArgumentException("Cover photo key is required.", nameof(coverPhotoKey));
        CoverPhotoKey = coverPhotoKey;
    }

    /// <summary>Max work-evidence photos an artisan can showcase.</summary>
    public const int MaxGalleryPhotos = 12;

    /// <summary>Adds an uploaded work-evidence photo (newest first).</summary>
    public void AddGalleryPhoto(string photoKey)
    {
        if (string.IsNullOrWhiteSpace(photoKey))
            throw new ArgumentException("Photo key is required.", nameof(photoKey));
        if (GalleryPhotoKeys.Count >= MaxGalleryPhotos)
            throw new InvalidOperationException(
                $"The gallery is full ({MaxGalleryPhotos} photos). Delete one to add another.");
        // Re-assign so EF's change tracking sees a new list instance.
        GalleryPhotoKeys = GalleryPhotoKeys.Prepend(photoKey).ToList();
    }

    /// <summary>Removes a gallery photo by key. False when the key isn't ours.</summary>
    public bool RemoveGalleryPhoto(string photoKey)
    {
        if (!GalleryPhotoKeys.Contains(photoKey)) return false;
        GalleryPhotoKeys = GalleryPhotoKeys.Where(k => k != photoKey).ToList();
        return true;
    }

    /// <summary>Attaches an uploaded work certificate (optional trust signal).</summary>
    public void SetCertificate(string certificateKey)
    {
        if (string.IsNullOrWhiteSpace(certificateKey))
            throw new ArgumentException("Certificate key is required.", nameof(certificateKey));
        CertificateKey = certificateKey;
    }

    /// <summary>True when the artisan uploaded a work certificate.</summary>
    public bool HasCertificate => !string.IsNullOrEmpty(CertificateKey);

    /// <summary>
    /// Composite ranking score for catalogue ordering: the rating average leads,
    /// and a certificate adds a small boost — so a certified newcomer starts
    /// above an uncertified one, while real customer ratings dominate over time.
    /// </summary>
    public double RankScore => Rating + (HasCertificate ? 0.5 : 0);

    /// <summary>Toggles the artisan's availability (online/offline).</summary>
    public void SetAvailability(bool available) => IsAvailable = available;

    /// <summary>Soft-delete the profile (hidden from the marketplace, recoverable).</summary>
    public void SoftDelete(DateTimeOffset now) => DeletedAtUtc ??= now;

    /// <summary>Undo a soft-delete.</summary>
    public void Restore() => DeletedAtUtc = null;

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
