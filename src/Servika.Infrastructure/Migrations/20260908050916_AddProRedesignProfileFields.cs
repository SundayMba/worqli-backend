using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProRedesignProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsEmergency",
                table: "artisan_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AwayUntilUtc",
                table: "artisan_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountName",
                table: "artisan_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountNumber",
                table: "artisan_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankCode",
                table: "artisan_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankName",
                table: "artisan_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkRadiusKm",
                table: "artisan_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<string>(
                name: "WorkingHoursJson",
                table: "artisan_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "artisan_guarantors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    YearsKnown = table.Column<int>(type: "integer", nullable: false),
                    Occupation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IdPhotoKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artisan_guarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_artisan_guarantors_users_ArtisanUserId",
                        column: x => x.ArtisanUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artisan_guarantors_ArtisanUserId",
                table: "artisan_guarantors",
                column: "ArtisanUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artisan_guarantors");

            migrationBuilder.DropColumn(
                name: "AcceptsEmergency",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "AwayUntilUtc",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutAccountName",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutAccountNumber",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutBankCode",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "PayoutBankName",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "WorkRadiusKm",
                table: "artisan_profiles");

            migrationBuilder.DropColumn(
                name: "WorkingHoursJson",
                table: "artisan_profiles");
        }
    }
}
