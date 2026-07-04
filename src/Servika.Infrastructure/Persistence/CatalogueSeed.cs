using Microsoft.EntityFrameworkCore;
using Servika.Domain.Catalogue;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// Seeds the marketplace catalogue (categories + a few artisan profiles) via EF
/// Core <c>HasData</c>, so a fresh database has browsable content immediately.
/// Ids are fixed/deterministic — required by HasData and stable across migrations.
///
/// Each seeded artisan profile is now backed by a real, verified <c>User</c>
/// account (role <c>Artisan</c>) so an artisan can sign in and act on the jobs
/// assigned to them. These are <b>dev/launch seed credentials</b> — see
/// <see cref="ArtisanLoginPassword"/>.
/// </summary>
internal static class CatalogueSeed
{
    private static Guid CategoryId(int n) => new($"a0000000-0000-0000-0000-{n:000000000000}");
    private static Guid ArtisanId(int n) => new($"b0000000-0000-0000-0000-{n:000000000000}");
    private static Guid ArtisanUserId(int n) => new($"c0000000-0000-0000-0000-{n:000000000000}");

    /// <summary>The seeded platform admin (resolves disputes; dev/test only).</summary>
    private static readonly Guid AdminUserId = new("e0000000-0000-0000-0000-000000000001");

    /// <summary>Shared password for all seeded artisan accounts (dev/test only).</summary>
    public const string ArtisanLoginPassword = "Servika123!";

    // Pre-computed BCrypt hash of <see cref="ArtisanLoginPassword"/> at work factor
    // 12 (matching BCryptPasswordHasher). A constant is required because HasData
    // seed values must be deterministic — a freshly salted hash would differ on
    // every model build and force phantom migrations.
    private const string ArtisanPasswordHash =
        "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG";

    // Fixed instant for all seeded timestamps (HasData must be deterministic).
    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(ArtisanUsers());
        modelBuilder.Entity<User>().HasData(AdminUsers());
        modelBuilder.Entity<ServiceCategory>().HasData(Categories());
        modelBuilder.Entity<ArtisanProfile>().HasData(Artisans());
    }

    // The platform admin account that resolves disputes. Role stored as a string
    // by the value converter; shares the deterministic seed password hash.
    private static object[] AdminUsers() =>
    [
        new
        {
            Id = AdminUserId,
            FullName = "Servika Admin",
            Email = "admin@servika.test",
            PhoneNumber = "+2348100000009",
            PasswordHash = ArtisanPasswordHash,
            Role = Role.Admin,
            CreatedAt = SeedTime,
            EmailVerifiedAtUtc = (DateTimeOffset?)SeedTime,
        },
    ];

    // The login accounts behind the three seeded artisan profiles. Anonymous
    // objects (matched by property name) keep User's setters private; Role is
    // stored as a string by the configured value converter.
    private static object[] ArtisanUsers() =>
    [
        new
        {
            Id = ArtisanUserId(1),
            FullName = "Emeka Okafor",
            Email = "emeka.okafor@artisan.servika.test",
            PhoneNumber = "+2348100000001",
            PasswordHash = ArtisanPasswordHash,
            Role = Role.Artisan,
            CreatedAt = SeedTime,
            EmailVerifiedAtUtc = (DateTimeOffset?)SeedTime,
        },
        new
        {
            Id = ArtisanUserId(2),
            FullName = "Ibrahim Yusuf",
            Email = "ibrahim.yusuf@artisan.servika.test",
            PhoneNumber = "+2348100000002",
            PasswordHash = ArtisanPasswordHash,
            Role = Role.Artisan,
            CreatedAt = SeedTime,
            EmailVerifiedAtUtc = (DateTimeOffset?)SeedTime,
        },
        new
        {
            Id = ArtisanUserId(3),
            FullName = "Chidi Okeke",
            Email = "chidi.okeke@artisan.servika.test",
            PhoneNumber = "+2348100000003",
            PasswordHash = ArtisanPasswordHash,
            Role = Role.Artisan,
            CreatedAt = SeedTime,
            EmailVerifiedAtUtc = (DateTimeOffset?)SeedTime,
        },
    ];

    private static ServiceCategory[] Categories()
    {
        // (slug, name, tint, isPopular, iconKey). Order here is the display order.
        var rows = new (string Slug, string Name, string Tint, bool Popular, string? Icon)[]
        {
            ("electrical", "Electrical", "#F59E0B", true, null),
            ("plumbing", "Plumbing", "#3B82F6", true, null),
            ("ac", "AC Repair", "#0EA5E9", true, null),
            ("fridge", "Fridge Repair", "#06B6D4", true, null),
            ("generator", "Generator", "#8B5CF6", true, null),
            ("solar", "Solar", "#F97316", true, null),
            ("painting", "Painting", "#EC4899", true, null),
            ("carpentry", "Carpentry", "#A16207", true, null),
            ("appliance", "Appliance Repair", "#14B8A6", false, null),
            ("cleaning", "Cleaning", "#22C55E", false, null),
            ("welding", "Welding", "#EF4444", false, null),
            ("tiling", "Tiling", "#6366F1", false, null),
            ("roofing", "Roofing", "#0EA5E9", false, null),
            ("security", "CCTV & Security", "#64748B", false, null),
            ("pest-control", "Pest Control", "#16A34A", false, null),
            ("locksmith", "Locksmith", "#CA8A04", false, null),
            ("electronics", "Electronics", "#3B82F6", false, null),
            ("satellite", "Satellite & TV", "#0891B2", false, null),
            ("plastering", "Plastering", "#A16207", false, null),
            ("water-pump", "Water Pumps", "#2563EB", false, null),
            ("vulcanizer", "Vulcanizer", "#334155", false, "tire"),
        };

        return rows
            .Select((r, i) => ServiceCategory.Create(
                id: CategoryId(i + 1),
                slug: r.Slug,
                name: r.Name,
                tint: r.Tint,
                sortOrder: i + 1,
                isPopular: r.Popular,
                iconKey: r.Icon))
            .ToArray();
    }

    private static ArtisanProfile[] Artisans() =>
    [
        ArtisanProfile.Create(
            id: ArtisanId(1),
            userId: ArtisanUserId(1),
            imageKey: "emeka-okafor",
            fullName: "Emeka Okafor",
            specialty: "Electrical Specialist",
            rating: 4.8,
            reviewCount: 124,
            distanceKm: 1.2,
            isAvailable: true,
            accent: "#F97316",
            experienceYears: 6,
            location: "Lagos, Nigeria",
            responseTime: "15 min",
            jobsCount: "120+",
            inspectionFeeNaira: 5000,
            about: "Professional electrician specializing in installations, repairs and maintenance. Committed to quality work and customer safety.",
            categorySlugs: ["electrical"],
            services: ["Installation", "Repair", "Maintenance", "Wiring"],
            galleryKeys: ["electrician", "hvac", "fridge", "carpenter"],
            latitude: 6.4478,   // Lekki, Lagos
            longitude: 3.4723),

        ArtisanProfile.Create(
            id: ArtisanId(2),
            userId: ArtisanUserId(2),
            imageKey: "ibrahim-yusuf",
            fullName: "Ibrahim Yusuf",
            specialty: "Plumbing Expert",
            rating: 4.9,
            reviewCount: 98,
            distanceKm: 2.3,
            isAvailable: true,
            accent: "#3B82F6",
            experienceYears: 8,
            location: "Lagos, Nigeria",
            responseTime: "10 min",
            jobsCount: "200+",
            inspectionFeeNaira: 4000,
            about: "Experienced plumber handling installations, leak repairs and pipe maintenance. Reliable, neat and available for emergencies.",
            categorySlugs: ["plumbing"],
            services: ["Leak Repair", "Installation", "Drainage", "Maintenance"],
            galleryKeys: ["plumber", "fridge", "electrician", "carpenter"],
            latitude: 6.5095,   // Yaba, Lagos
            longitude: 3.3711),

        ArtisanProfile.Create(
            id: ArtisanId(3),
            userId: ArtisanUserId(3),
            imageKey: "chidi-okeke",
            fullName: "Chidi Okeke",
            specialty: "AC Technician",
            rating: 4.7,
            reviewCount: 76,
            distanceKm: 3.1,
            isAvailable: false,
            accent: "#10B981",
            experienceYears: 5,
            location: "Lagos, Nigeria",
            responseTime: "20 min",
            jobsCount: "90+",
            inspectionFeeNaira: 6000,
            about: "Certified HVAC technician for AC servicing, installation and gas refill. Focused on efficient cooling and long-term reliability.",
            categorySlugs: ["ac", "fridge"],
            services: ["AC Servicing", "Installation", "Gas Refill", "Repair"],
            galleryKeys: ["hvac", "fridge", "electrician", "plumber"],
            latitude: 6.6018,   // Ikeja, Lagos
            longitude: 3.3515),
    ];
}
