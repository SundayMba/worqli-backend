using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnoseAndFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovedRepairsNaira",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DiagnosisFeeNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaidNaira",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing bookings: what is already held is the sum of their settled payments.
            migrationBuilder.Sql(@"UPDATE bookings b SET ""PaidNaira"" = COALESCE((
                SELECT SUM(p.""AmountNaira"") FROM payments p
                WHERE p.""BookingId"" = b.""Id"" AND p.""Status"" IN ('Succeeded', 'Refunded', 'PartiallyRefunded')), 0);");

            migrationBuilder.CreateTable(
                name: "booking_repairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceBookItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LabourNaira = table.Column<int>(type: "integer", nullable: false),
                    PartsNaira = table.Column<int>(type: "integer", nullable: false),
                    PartsNote = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PhotoKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProposedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_repairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_repairs_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_repairs_BookingId",
                table: "booking_repairs",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_repairs");

            migrationBuilder.DropColumn(
                name: "ApprovedRepairsNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DiagnosisFeeNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "PaidNaira",
                table: "bookings");
        }
    }
}
