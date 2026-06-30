using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtisanAccountsAndTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "artisan_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000001"),
                column: "UserId",
                value: new Guid("c0000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000002"),
                column: "UserId",
                value: new Guid("c0000000-0000-0000-0000-000000000002"));

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000003"),
                column: "UserId",
                value: new Guid("c0000000-0000-0000-0000-000000000003"));

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "Email", "EmailVerifiedAtUtc", "FullName", "PasswordHash", "PhoneNumber", "Role" },
                values: new object[,]
                {
                    { new Guid("c0000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "emeka.okafor@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Emeka Okafor", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000001", "Artisan" },
                    { new Guid("c0000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ibrahim.yusuf@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ibrahim Yusuf", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000002", "Artisan" },
                    { new Guid("c0000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "chidi.okeke@artisan.servika.test", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Chidi Okeke", "$2a$12$nPLo4sZMl89KcvqqJUMcHuUgfVOIeVcVLaBqejV9sQjGqT7X8IriG", "+2348100000003", "Artisan" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_profiles_UserId",
                table: "artisan_profiles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_artisan_profiles_users_UserId",
                table: "artisan_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_artisan_profiles_users_UserId",
                table: "artisan_profiles");

            migrationBuilder.DropIndex(
                name: "IX_artisan_profiles_UserId",
                table: "artisan_profiles");

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("c0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "artisan_profiles");

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
