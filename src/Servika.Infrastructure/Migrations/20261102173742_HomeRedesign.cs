using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HomeRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_interests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AreaLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_interests", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "Name",
                value: "Plumbing & Borehole");

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "IsPopular",
                value: false);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "Name",
                value: "Solar & Inverter");

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "IsPopular",
                value: false);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "IsPopular",
                value: false);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000020"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000021"),
                column: "IsActive",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_service_interests_CreatedAtUtc",
                table: "service_interests",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_interests");

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "Name",
                value: "Plumbing");

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "IsPopular",
                value: true);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "Name",
                value: "Solar");

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "IsPopular",
                value: true);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"),
                column: "IsPopular",
                value: true);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000020"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "service_categories",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000021"),
                column: "IsActive",
                value: true);
        }
    }
}
