using Microsoft.EntityFrameworkCore;
using Servika.Domain.Catalogue;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// Seeds the marketplace catalogue (categories + a few artisan profiles) via EF
/// Core <c>HasData</c>, so a fresh database has browsable content immediately.
/// Ids are fixed/deterministic — required by HasData and stable across migrations.
/// In a later slice artisans become real, user-linked accounts; this is launch
/// reference data the customer app reads.
/// </summary>
internal static class CatalogueSeed
{
    private static Guid CategoryId(int n) => new($"a0000000-0000-0000-0000-{n:000000000000}");
    private static Guid ArtisanId(int n) => new($"b0000000-0000-0000-0000-{n:000000000000}");

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceCategory>().HasData(Categories());
        modelBuilder.Entity<ArtisanProfile>().HasData(Artisans());
    }

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
            galleryKeys: ["electrician", "hvac", "fridge", "carpenter"]),

        ArtisanProfile.Create(
            id: ArtisanId(2),
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
            galleryKeys: ["plumber", "fridge", "electrician", "carpenter"]),

        ArtisanProfile.Create(
            id: ArtisanId(3),
            imageKey: "chidi-okeke",
            fullName: "Chidi Okeke",
            specialty: "AC Technician",
            rating: 4.7,
            reviewCount: 76,
            distanceKm: 3.1,
            isAvailable: false,
            accent: "#10B981",
            experienceYears: 5,
            location: "Abuja, Nigeria",
            responseTime: "20 min",
            jobsCount: "90+",
            inspectionFeeNaira: 6000,
            about: "Certified HVAC technician for AC servicing, installation and gas refill. Focused on efficient cooling and long-term reliability.",
            categorySlugs: ["ac", "fridge"],
            services: ["AC Servicing", "Installation", "Gas Refill", "Repair"],
            galleryKeys: ["hvac", "fridge", "electrician", "plumber"]),
    ];
}
