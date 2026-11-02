using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCodesAndSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Direct");

            migrationBuilder.AddColumn<string>(
                name: "BookingCode",
                table: "artisan_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisan_profiles_BookingCode",
                table: "artisan_profiles",
                column: "BookingCode",
                unique: true,
                filter: "\"BookingCode\" IS NOT NULL");

            // Existing rows: a request with no artisan was found by rotation (the old broadcast), the rest were chosen directly.
            migrationBuilder.Sql("UPDATE bookings SET \"Source\" = 'Rotation' WHERE \"ArtisanId\" IS NULL;");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_artisan_profiles_BookingCode",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BookingCode",
                table: "artisan_profiles");
        }
    }
}
