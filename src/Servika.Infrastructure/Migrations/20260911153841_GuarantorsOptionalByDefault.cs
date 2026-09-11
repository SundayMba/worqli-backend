using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <summary>
    /// Guarantors are recommended, not required, by default (user decision 2026-09-11).
    /// Flips the existing settings row and the column default; the admin switch stays.
    /// </summary>
    [DbContext(typeof(Persistence.ServikaDbContext))]
    [Migration("20260911153841_GuarantorsOptionalByDefault")]
    public partial class GuarantorsOptionalByDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "RequireGuarantors",
                table: "platform_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
            migrationBuilder.Sql("UPDATE platform_settings SET \"RequireGuarantors\" = FALSE;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "RequireGuarantors",
                table: "platform_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
