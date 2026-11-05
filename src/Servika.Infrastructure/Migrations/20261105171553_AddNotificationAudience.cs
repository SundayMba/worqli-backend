using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "App",
                table: "push_tokens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "notifications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Any");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "App",
                table: "push_tokens");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "notifications");
        }
    }
}
