using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuarantorContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactNote",
                table: "artisan_guarantors",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactStatus",
                table: "artisan_guarantors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotCalled");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContactedAtUtc",
                table: "artisan_guarantors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactedByUserId",
                table: "artisan_guarantors",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactNote",
                table: "artisan_guarantors");

            migrationBuilder.DropColumn(
                name: "ContactStatus",
                table: "artisan_guarantors");

            migrationBuilder.DropColumn(
                name: "ContactedAtUtc",
                table: "artisan_guarantors");

            migrationBuilder.DropColumn(
                name: "ContactedByUserId",
                table: "artisan_guarantors");
        }
    }
}
