using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceBidding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "bids",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Price");

            migrationBuilder.AddColumn<int>(
                name: "VisitFeeNaira",
                table: "bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CategorySlug",
                table: "artisan_services",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Review",
                table: "artisan_services",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "artisan_services",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "artisan_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "artisan_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAtUtc",
                table: "artisan_services",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "PendingGalleryPhotoKeys",
                table: "artisan_profiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "estate_enquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContactRole = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UnitCount = table.Column<int>(type: "integer", nullable: true),
                    ServicesWanted = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    Frequency = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estate_enquiries", x => x.Id);
                });

            // Existing posts sit under their artisan's first trade until the artisan picks another.
            migrationBuilder.Sql(@"UPDATE artisan_services s SET ""CategorySlug"" = COALESCE(a.""CategorySlugs""[1], '')
                FROM artisan_profiles a WHERE a.""Id"" = s.""ArtisanProfileId"" AND s.""CategorySlug"" = '';");
            // Open requests that were waiting under rotation are now open for bids.
            migrationBuilder.Sql(@"UPDATE bookings SET ""Assessment"" = 'RemoteQuote' WHERE ""Status"" = 'Open' AND ""ArtisanId"" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_artisan_services_Review",
                table: "artisan_services",
                column: "Review");

            migrationBuilder.CreateIndex(
                name: "IX_estate_enquiries_CreatedAtUtc",
                table: "estate_enquiries",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estate_enquiries");

            migrationBuilder.DropIndex(
                name: "IX_artisan_services_Review",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "VisitFeeNaira",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "CategorySlug",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "Review",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "artisan_services");

            migrationBuilder.DropColumn(
                name: "PendingGalleryPhotoKeys",
                table: "artisan_profiles");
        }
    }
}
