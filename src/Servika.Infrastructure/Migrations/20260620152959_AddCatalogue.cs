using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artisan_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    DistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Accent = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ResponseTime = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JobsCount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InspectionFeeNaira = table.Column<int>(type: "integer", nullable: false),
                    About = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CategorySlugs = table.Column<List<string>>(type: "text[]", nullable: false),
                    Services = table.Column<List<string>>(type: "text[]", nullable: false),
                    GalleryKeys = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisan_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tint = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "artisan_profiles",
                columns: new[] { "Id", "About", "Accent", "CategorySlugs", "DistanceKm", "ExperienceYears", "FullName", "GalleryKeys", "ImageKey", "InspectionFeeNaira", "IsAvailable", "JobsCount", "Location", "Rating", "ResponseTime", "ReviewCount", "Services", "Specialty" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000001"), "Professional electrician specializing in installations, repairs and maintenance. Committed to quality work and customer safety.", "#F97316", new List<string> { "electrical" }, 1.2, 6, "Emeka Okafor", new List<string> { "electrician", "hvac", "fridge", "carpenter" }, "emeka-okafor", 5000, true, "120+", "Lagos, Nigeria", 4.7999999999999998, "15 min", 124, new List<string> { "Installation", "Repair", "Maintenance", "Wiring" }, "Electrical Specialist" },
                    { new Guid("b0000000-0000-0000-0000-000000000002"), "Experienced plumber handling installations, leak repairs and pipe maintenance. Reliable, neat and available for emergencies.", "#3B82F6", new List<string> { "plumbing" }, 2.2999999999999998, 8, "Ibrahim Yusuf", new List<string> { "plumber", "fridge", "electrician", "carpenter" }, "ibrahim-yusuf", 4000, true, "200+", "Lagos, Nigeria", 4.9000000000000004, "10 min", 98, new List<string> { "Leak Repair", "Installation", "Drainage", "Maintenance" }, "Plumbing Expert" },
                    { new Guid("b0000000-0000-0000-0000-000000000003"), "Certified HVAC technician for AC servicing, installation and gas refill. Focused on efficient cooling and long-term reliability.", "#10B981", new List<string> { "ac", "fridge" }, 3.1000000000000001, 5, "Chidi Okeke", new List<string> { "hvac", "fridge", "electrician", "plumber" }, "chidi-okeke", 6000, false, "90+", "Abuja, Nigeria", 4.7000000000000002, "20 min", 76, new List<string> { "AC Servicing", "Installation", "Gas Refill", "Repair" }, "AC Technician" }
                });

            migrationBuilder.InsertData(
                table: "service_categories",
                columns: new[] { "Id", "IconKey", "IsActive", "IsPopular", "Name", "Slug", "SortOrder", "Tint" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), null, true, true, "Electrical", "electrical", 1, "#F59E0B" },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), null, true, true, "Plumbing", "plumbing", 2, "#3B82F6" },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), null, true, true, "AC Repair", "ac", 3, "#0EA5E9" },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), null, true, true, "Fridge Repair", "fridge", 4, "#06B6D4" },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), null, true, true, "Generator", "generator", 5, "#8B5CF6" },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), null, true, true, "Solar", "solar", 6, "#F97316" },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), null, true, true, "Painting", "painting", 7, "#EC4899" },
                    { new Guid("a0000000-0000-0000-0000-000000000008"), null, true, true, "Carpentry", "carpentry", 8, "#A16207" },
                    { new Guid("a0000000-0000-0000-0000-000000000009"), null, true, false, "Appliance Repair", "appliance", 9, "#14B8A6" },
                    { new Guid("a0000000-0000-0000-0000-000000000010"), null, true, false, "Cleaning", "cleaning", 10, "#22C55E" },
                    { new Guid("a0000000-0000-0000-0000-000000000011"), null, true, false, "Welding", "welding", 11, "#EF4444" },
                    { new Guid("a0000000-0000-0000-0000-000000000012"), null, true, false, "Tiling", "tiling", 12, "#6366F1" },
                    { new Guid("a0000000-0000-0000-0000-000000000013"), null, true, false, "Roofing", "roofing", 13, "#0EA5E9" },
                    { new Guid("a0000000-0000-0000-0000-000000000014"), null, true, false, "CCTV & Security", "security", 14, "#64748B" },
                    { new Guid("a0000000-0000-0000-0000-000000000015"), null, true, false, "Pest Control", "pest-control", 15, "#16A34A" },
                    { new Guid("a0000000-0000-0000-0000-000000000016"), null, true, false, "Locksmith", "locksmith", 16, "#CA8A04" },
                    { new Guid("a0000000-0000-0000-0000-000000000017"), null, true, false, "Electronics", "electronics", 17, "#3B82F6" },
                    { new Guid("a0000000-0000-0000-0000-000000000018"), null, true, false, "Satellite & TV", "satellite", 18, "#0891B2" },
                    { new Guid("a0000000-0000-0000-0000-000000000019"), null, true, false, "Plastering", "plastering", 19, "#A16207" },
                    { new Guid("a0000000-0000-0000-0000-000000000020"), null, true, false, "Water Pumps", "water-pump", 20, "#2563EB" },
                    { new Guid("a0000000-0000-0000-0000-000000000021"), "tire", true, false, "Vulcanizer", "vulcanizer", 21, "#334155" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_profiles_CategorySlugs",
                table: "artisan_profiles",
                column: "CategorySlugs")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_Slug",
                table: "service_categories",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artisan_profiles");

            migrationBuilder.DropTable(
                name: "service_categories");
        }
    }
}
