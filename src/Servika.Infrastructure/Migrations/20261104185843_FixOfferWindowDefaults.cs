using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixOfferWindowDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddJobOffers gave the existing settings row 0 for both windows (its column default), which made a
            // direct offer lapse at once and blocked saving Settings in the console (1..180 / 1..1440 enforced).
            migrationBuilder.Sql(@"UPDATE platform_settings SET ""OfferWindowMinutes"" = 10 WHERE ""OfferWindowMinutes"" < 1;");
            migrationBuilder.Sql(@"UPDATE platform_settings SET ""DirectOfferWindowMinutes"" = 60 WHERE ""DirectOfferWindowMinutes"" < 1;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
