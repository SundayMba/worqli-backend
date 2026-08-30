using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemisedQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgreedMaterialsNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgreedWorkmanshipNaira",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Materials",
                table: "bids",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "WorkmanshipNaira",
                table: "bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing bids were single-price: treat that price as all workmanship.
            migrationBuilder.Sql("UPDATE bids SET \"WorkmanshipNaira\" = \"AmountNaira\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreedMaterialsNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "AgreedWorkmanshipNaira",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Materials",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "WorkmanshipNaira",
                table: "bids");
        }
    }
}
