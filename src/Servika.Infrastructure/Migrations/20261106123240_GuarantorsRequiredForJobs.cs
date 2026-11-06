using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuarantorsRequiredForJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarantors now gate taking work (not applying); the rule is on for everyone. Admins can switch it off.
            migrationBuilder.Sql("UPDATE platform_settings SET \"RequireGuarantors\" = true;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE platform_settings SET \"RequireGuarantors\" = false;");

        }
    }
}
