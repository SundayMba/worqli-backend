using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeWithdrawalOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_withdrawals_ArtisanId",
                table: "withdrawals");

            migrationBuilder.RenameColumn(
                name: "ArtisanId",
                table: "withdrawals",
                newName: "OwnerId");

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                table: "withdrawals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every existing withdrawal was an artisan payout (OwnerId already holds
            // the artisan profile id via the rename above).
            migrationBuilder.Sql(
                "UPDATE withdrawals SET \"OwnerType\" = 'Artisan' WHERE \"OwnerType\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_withdrawals_OwnerType_OwnerId",
                table: "withdrawals",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_withdrawals_OwnerType_OwnerId",
                table: "withdrawals");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "withdrawals");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "withdrawals",
                newName: "ArtisanId");

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

            migrationBuilder.CreateIndex(
                name: "IX_withdrawals_ArtisanId",
                table: "withdrawals",
                column: "ArtisanId");
        }
    }
}
