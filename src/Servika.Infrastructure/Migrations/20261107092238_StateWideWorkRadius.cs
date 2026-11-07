using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StateWideWorkRadius : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "WorkRadiusKm",
                table: "artisan_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 80,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 8);

            // Artisans still on the old 8 km default now hear about jobs across the state (80 km).
            migrationBuilder.Sql("UPDATE artisan_profiles SET \"WorkRadiusKm\" = 80 WHERE \"WorkRadiusKm\" = 8;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "WorkRadiusKm",
                table: "artisan_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 8,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 80);
        }
    }
}
