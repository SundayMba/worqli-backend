using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtisanKyc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artisan_kyc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SelfieKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdDocumentKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisan_kyc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artisan_kyc_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_kyc_UserId",
                table: "artisan_kyc",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artisan_kyc");

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
