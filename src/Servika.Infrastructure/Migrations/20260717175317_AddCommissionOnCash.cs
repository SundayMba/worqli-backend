using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionOnCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxCommissionDebtNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing settings row gets the same default a fresh marketplace
            // starts with (PlatformSettings.Default) — 0 would restrict artisans
            // on any kobo of debt the moment commission switches on.
            migrationBuilder.Sql(
                "UPDATE platform_settings SET \"MaxCommissionDebtNaira\" = 2000;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookingId",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "payments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "BookingEscrow");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxCommissionDebtNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookingId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "GalleryPhotoKeys", "Services" },
                values: new object[] { new List<string> { "electrical" }, new List<string> { "electrician", "hvac", "fridge", "carpenter" }, new List<string>(), new List<string> { "Installation", "Repair", "Maintenance", "Wiring" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000002"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "GalleryPhotoKeys", "Services" },
                values: new object[] { new List<string> { "plumbing" }, new List<string> { "plumber", "fridge", "electrician", "carpenter" }, new List<string>(), new List<string> { "Leak Repair", "Installation", "Drainage", "Maintenance" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000003"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "GalleryPhotoKeys", "Services" },
                values: new object[] { new List<string> { "ac", "fridge" }, new List<string> { "hvac", "fridge", "electrician", "plumber" }, new List<string>(), new List<string> { "AC Servicing", "Installation", "Gas Refill", "Repair" } });
        }
    }
}
