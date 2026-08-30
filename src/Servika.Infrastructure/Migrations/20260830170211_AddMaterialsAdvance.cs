using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialsAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MaterialsAdvanceDecidedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialsAdvanceNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MaterialsAdvanceRequestedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialsAdvanceStatus",
                table: "bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialsAdvanceDecidedAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "MaterialsAdvanceNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "MaterialsAdvanceRequestedAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "MaterialsAdvanceStatus",
                table: "bookings");
        }
    }
}
