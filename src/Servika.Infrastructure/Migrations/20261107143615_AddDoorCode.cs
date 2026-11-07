using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DoorCode",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoorCodeAttempts",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DoorCodeConfirmedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DoorCodeLockedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoorCode",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DoorCodeAttempts",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DoorCodeConfirmedAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DoorCodeLockedAtUtc",
                table: "bookings");
        }
    }
}
