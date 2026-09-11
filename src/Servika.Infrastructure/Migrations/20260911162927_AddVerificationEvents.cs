using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OpenCheck",
                table: "artisan_kyc",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenNote",
                table: "artisan_kyc",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenReasonCode",
                table: "artisan_kyc",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResubmissionCount",
                table: "artisan_kyc",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResubmittedAtUtc",
                table: "artisan_kyc",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "verification_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KycId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Check = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verification_events_artisan_kyc_KycId",
                        column: x => x.KycId,
                        principalTable: "artisan_kyc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verification_events_KycId_CreatedAtUtc",
                table: "verification_events",
                columns: new[] { "KycId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verification_events");

            migrationBuilder.DropColumn(
                name: "OpenCheck",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "OpenNote",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "OpenReasonCode",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "ResubmissionCount",
                table: "artisan_kyc");

            migrationBuilder.DropColumn(
                name: "ResubmittedAtUtc",
                table: "artisan_kyc");
        }
    }
}
