using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKycConsentAndLiveness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SafetyAcknowledgedAtUtc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentVersion",
                table: "artisan_kyc",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentedAtUtc",
                table: "artisan_kyc",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdFaceFound",
                table: "artisan_kyc",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdNumberFound",
                table: "artisan_kyc",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdNumberMatches",
                table: "artisan_kyc",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdTextFound",
                table: "artisan_kyc",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LivenessAttempts",
                table: "artisan_kyc",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "LivenessChallenges",
                table: "artisan_kyc",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "LivenessMethod",
                table: "artisan_kyc",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LivenessPassed",
                table: "artisan_kyc",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SelfieImagesDeletedAtUtc",
                table: "artisan_kyc",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_artisan_kyc_Status_ReviewedAtUtc",
                table: "artisan_kyc",
                columns: new[] { "Status", "ReviewedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_artisan_kyc_Status_ReviewedAtUtc",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "SafetyAcknowledgedAtUtc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "ConsentVersion",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "ConsentedAtUtc",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "IdFaceFound",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "IdNumberFound",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "IdNumberMatches",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "IdTextFound",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "LivenessAttempts",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "LivenessChallenges",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "LivenessMethod",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "LivenessPassed",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "SelfieImagesDeletedAtUtc",
                table: "artisan_kyc");
        }
    }
}
