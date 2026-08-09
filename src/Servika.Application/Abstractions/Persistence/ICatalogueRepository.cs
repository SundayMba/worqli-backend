using Servika.Domain.Catalogue;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Read access to the marketplace catalogue (categories + artisan profiles).
/// Stated in domain terms; Infrastructure implements it with EF Core. All
/// queries are read-only for the catalogue slice.
/// </summary>
public interface ICatalogueRepository
{
    /// <summary>Active categories in display order.</summary>
    Task<IReadOnlyList<ServiceCategory>> GetCategoriesAsync(CancellationToken ct);

    /// <summary>True if a category with this slug exists and is active.</summary>
    Task<bool> CategoryExistsAsync(string slug, CancellationToken ct);

    /// <summary>A single active category by slug, or null — used when a booking
    /// needs the category's display name.</summary>
    Task<ServiceCategory?> GetCategoryBySlugAsync(string slug, CancellationToken ct);

    /// <summary>Artisan summaries, optionally filtered to one category slug.</summary>
    Task<IReadOnlyList<ArtisanProfile>> GetArtisansAsync(string? categorySlug, CancellationToken ct);

    /// <summary>Every artisan profile regardless of verification status — the admin
    /// directory (newest first). Public catalogue reads use <see cref="GetArtisansAsync"/>.</summary>
    Task<IReadOnlyList<ArtisanProfile>> ListAllArtisansAsync(CancellationToken ct);

    /// <summary>A single artisan profile by id, or null if not found.</summary>
    Task<ArtisanProfile?> GetArtisanByIdAsync(Guid id, CancellationToken ct);

    /// <summary>The artisan profile linked to a login account, or null if this
    /// user has no artisan profile. Used to resolve a signed-in artisan to the
    /// jobs assigned to their profile.</summary>
    Task<ArtisanProfile?> GetArtisanByUserIdAsync(Guid userId, CancellationToken ct);

    /// <summary>Available <b>verified</b> artisans (with a linked account) whose
    /// services include the given category — candidate recipients of an open-job
    /// broadcast. Carries the profile id too, so callers can filter out artisans
    /// whose standing (unpaid cash-job commission) blocks new requests.</summary>
    Task<IReadOnlyList<ArtisanRecipient>> ListArtisanRecipientsInCategoryAsync(string categorySlug, CancellationToken ct);

    /// <summary>A <b>tracked</b> artisan profile by id, or null — used when a
    /// review needs to fold its rating into the artisan's aggregate (the change
    /// must persist on SaveChanges). The read-only <see cref="GetArtisanByIdAsync"/>
    /// is no-tracking and can't.</summary>
    Task<ArtisanProfile?> FindArtisanForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The <b>tracked</b> profile linked to a login account (for onboarding
    /// edits), or null if this user has no profile yet.</summary>
    Task<ArtisanProfile?> GetArtisanByUserIdForUpdateAsync(Guid userId, CancellationToken ct);

    /// <summary>Tracked profile-by-user lookup INCLUDING a soft-deleted profile, so an
    /// account soft-delete/restore can flip the profile's state too.</summary>
    Task<ArtisanProfile?> GetArtisanByUserIdForUpdateIncludingDeletedAsync(Guid userId, CancellationToken ct);

    /// <summary>Stages a new artisan profile (self-onboarding). Persisted on SaveChanges.</summary>
    void AddArtisan(ArtisanProfile profile);

    // ── Admin category management ──────────────────────────────────────────

    /// <summary>All categories incl. inactive, in display order (admin view).</summary>
    Task<IReadOnlyList<ServiceCategory>> GetAllCategoriesAsync(CancellationToken ct);

    /// <summary>A category by id (tracked, so an admin edit/toggle persists), or null.</summary>
    Task<ServiceCategory?> FindCategoryByIdForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>True if any category (active or not) already uses this slug.</summary>
    Task<bool> CategorySlugExistsAsync(string slug, CancellationToken ct);

    /// <summary>Stages a new category. Persisted on SaveChanges.</summary>
    void AddCategory(ServiceCategory category);

    Task<int> SaveChangesAsync(CancellationToken ct);
}

/// <summary>A broadcast candidate: the artisan's profile id (standing/ledger key)
/// and their login account id (notification recipient).</summary>
public sealed record ArtisanRecipient(Guid ProfileId, Guid UserId);
