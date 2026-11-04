using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomPriceBookItemId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomQuote",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TypicalRangeMaxNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypicalRangeMinNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Review",
                table: "bids",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "NotNeeded");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "bids",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "bids",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "bids",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bids_Review",
                table: "bids",
                column: "Review");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bids_Review",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "CustomPriceBookItemId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "IsCustomQuote",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "TypicalRangeMaxNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "TypicalRangeMinNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Review",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "bids");
        }
    }
}
