using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CounterRounds",
                table: "bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingCounterNaira",
                table: "bids",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCounterNote",
                table: "bids",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CounterRounds",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "PendingCounterNaira",
                table: "bids");

            migrationBuilder.DropColumn(
                name: "PendingCounterNote",
                table: "bids");
        }
    }
}
