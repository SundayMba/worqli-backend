using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategorySlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ArtisanName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AddressText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LocationLat = table.Column<double>(type: "double precision", nullable: true),
                    LocationLng = table.Column<double>(type: "double precision", nullable: true),
                    LocationInstructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PreferredDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreferredTimeSlot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PricingModel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InitialQuoteAmountNaira = table.Column<int>(type: "integer", nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CustomerId",
                table: "bookings",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "electrical" }, new List<string> { "electrician", "hvac", "fridge", "carpenter" }, new List<string> { "Installation", "Repair", "Maintenance", "Wiring" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000002"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "plumbing" }, new List<string> { "plumber", "fridge", "electrician", "carpenter" }, new List<string> { "Leak Repair", "Installation", "Drainage", "Maintenance" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000003"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "ac", "fridge" }, new List<string> { "hvac", "fridge", "electrician", "plumber" }, new List<string> { "AC Servicing", "Installation", "Gas Refill", "Repair" } });
        }
    }
}
