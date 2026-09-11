using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoseSelfies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PoseSelfiesJson",
                table: "artisan_kyc",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PoseSelfiesJson",
                table: "artisan_kyc");
        }
    }
}
