using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuarantorPolicyAndNinLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireGuarantors",
                table: "platform_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredGuarantorCount",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "GuarantorsWaived",
                table: "artisan_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NinLookupAttemptsDayUtc",
                table: "artisan_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NinLookupAttemptsToday",
                table: "artisan_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NinLookupCheckedAtUtc",
                table: "artisan_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NinLookupName",
                table: "artisan_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NinLookupNumber",
                table: "artisan_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NinLookupStatus",
                table: "artisan_profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireGuarantors",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "RequiredGuarantorCount",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "GuarantorsWaived",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupAttemptsDayUtc",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupAttemptsToday",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupCheckedAtUtc",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupName",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupNumber",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "NinLookupStatus",
                table: "artisan_profiles");
        }
    }
}
