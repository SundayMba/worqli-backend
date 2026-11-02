using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DirectOfferWindowMinutes",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OfferWindowMinutes",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastOfferAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferRound",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OffersExhaustedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAssignedAtUtc",
                table: "artisan_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OfferedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_offers_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_ArtisanProfileId_Status",
                table: "job_offers",
                columns: new[] { "ArtisanProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_BookingId_Status",
                table: "job_offers",
                columns: new[] { "BookingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_Status_ExpiresAtUtc",
                table: "job_offers",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_offers");

            migrationBuilder.DropColumn(
                name: "DirectOfferWindowMinutes",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "OfferWindowMinutes",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "LastOfferAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "OfferRound",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "OffersExhaustedAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LastAssignedAtUtc",
                table: "artisan_profiles");
        }
    }
}
