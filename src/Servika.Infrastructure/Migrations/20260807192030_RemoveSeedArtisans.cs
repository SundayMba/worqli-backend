using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedArtisans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean the seeded artisans' data that has no cascading foreign key
            // (assigned jobs, wallet ledger, reviews/bids/favourites keyed by the
            // profile id, and artisan-side conversations) so removing the seed leaves
            // no orphans. Deleting their jobs cascades those jobs' payments, bids,
            // reviews, disputes and tracking via the booking foreign key.
            migrationBuilder.Sql(@"
DELETE FROM conversations WHERE ""ArtisanUserId"" IN ('c0000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000003');
DELETE FROM bookings WHERE ""ArtisanId"" IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003');
DELETE FROM reviews WHERE ""ArtisanId"" IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003');
DELETE FROM bids WHERE ""ArtisanId"" IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003');
DELETE FROM favorites WHERE ""ArtisanId"" IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003');
DELETE FROM wallet_transactions WHERE ""OwnerId"" IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000002','b0000000-0000-0000-0000-000000000003');
");

            migrationBuilder.DeleteData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000003"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "Email", "EmailVerifiedAtUtc", "FullName", "PasswordHash", "PhoneNumber", "PhoneVerifiedAtUtc", "ReferralCode", "Role", "SuspendedAtUtc" },
                values: new object[,]
                {
                    { new Guid("c0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "emeka.okafor@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Emeka Okafor", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000001", null, null, "Artisan", null },
                    { new Guid("c0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ibrahim.yusuf@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ibrahim Yusuf", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000002", null, null, "Artisan", null },
                    { new Guid("c0000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "chidi.okeke@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Chidi Okeke", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000003", null, null, "Artisan", null }
                });

            migrationBuilder.InsertData(
                table: "artisan_profiles",
                columns: new[] { "Id", "About", "Accent", "CategorySlugs", "CertificateKey", "CoverPhotoKey", "DistanceKm", "ExperienceYears", "FullName", "GalleryKeys", "GalleryPhotoKeys", "ImageKey", "InspectionFeeNaira", "IsAvailable", "JobsCount", "Latitude", "Location", "Longitude", "PhotoKey", "Rating", "ResponseTime", "ReviewCount", "Services", "Specialty", "UserId", "VerificationStatus" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000001"), "Professional electrician specializing in installations, repairs and maintenance. Committed to quality work and customer safety.", "#F97316", new List<string> { "electrical" }, null, null, 1.2, 6, "Emeka Okafor", new List<string> { "electrician", "hvac", "fridge", "carpenter" }, new List<string>(), "emeka-okafor", 5000, true, "120+", 6.4478, "Lagos, Nigeria", 3.4723000000000002, null, 4.7999999999999998, "15 min", 124, new List<string> { "Installation", "Repair", "Maintenance", "Wiring" }, "Electrical Specialist", new Guid("c0000000-0000-0000-0000-000000000001"), "Verified" },
                    { new Guid("b0000000-0000-0000-0000-000000000002"), "Experienced plumber handling installations, leak repairs and pipe maintenance. Reliable, neat and available for emergencies.", "#3B82F6", new List<string> { "plumbing" }, null, null, 2.2999999999999998, 8, "Ibrahim Yusuf", new List<string> { "plumber", "fridge", "electrician", "carpenter" }, new List<string>(), "ibrahim-yusuf", 4000, true, "200+", 6.5095000000000001, "Lagos, Nigeria", 3.3711000000000002, null, 4.9000000000000004, "10 min", 98, new List<string> { "Leak Repair", "Installation", "Drainage", "Maintenance" }, "Plumbing Expert", new Guid("c0000000-0000-0000-0000-000000000002"), "Verified" },
                    { new Guid("b0000000-0000-0000-0000-000000000003"), "Certified HVAC technician for AC servicing, installation and gas refill. Focused on efficient cooling and long-term reliability.", "#10B981", new List<string> { "ac", "fridge" }, null, null, 3.1000000000000001, 5, "Chidi Okeke", new List<string> { "hvac", "fridge", "electrician", "plumber" }, new List<string>(), "chidi-okeke", 6000, false, "90+", 6.6017999999999999, "Lagos, Nigeria", 3.3515000000000001, null, 4.7000000000000002, "20 min", 76, new List<string> { "AC Servicing", "Installation", "Gas Refill", "Repair" }, "AC Technician", new Guid("c0000000-0000-0000-0000-000000000003"), "Verified" }
                });
        }
    }
}
