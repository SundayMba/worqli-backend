using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSettingsAndSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspendedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    EmergencyCommissionRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    AutoConfirmHours = table.Column<int>(type: "integer", nullable: false),
                    MinWithdrawalNaira = table.Column<int>(type: "integer", nullable: false),
                    ReferralRewardNaira = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_settings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"),
                column: "SuspendedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000002"),
                column: "SuspendedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000003"),
                column: "SuspendedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("e0000000-0000-0000-0000-000000000001"),
                column: "SuspendedAtUtc",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "users");

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
