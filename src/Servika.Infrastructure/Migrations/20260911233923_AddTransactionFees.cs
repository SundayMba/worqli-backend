using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeeBearer",
                table: "withdrawals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Platform");

            migrationBuilder.AddColumn<int>(
                name: "FeeNaira",
                table: "withdrawals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CardFeeCapNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2000);

            migrationBuilder.AddColumn<int>(
                name: "CardFeeFlatFromNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2500);

            migrationBuilder.AddColumn<int>(
                name: "CardFeeFlatNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "CardFeeRate",
                table: "platform_settings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.015m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FeeNoticeSentAtUtc",
                table: "platform_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeeNoticeStage",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FeesStartAtUtc",
                table: "platform_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferFeeTier1MaxNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 5000);

            migrationBuilder.AddColumn<int>(
                name: "TransferFeeTier1Naira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "TransferFeeTier2MaxNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 50000);

            migrationBuilder.AddColumn<int>(
                name: "TransferFeeTier2Naira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "TransferFeeTier3Naira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "platform_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GatewayFeeNaira",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceFeeNaira",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeeBearer",
                table: "withdrawals");

            migrationBuilder.DropColumn(
                name: "FeeNaira",
                table: "withdrawals");

            migrationBuilder.DropColumn(
                name: "CardFeeCapNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "CardFeeFlatFromNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "CardFeeFlatNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "CardFeeRate",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "FeeNoticeSentAtUtc",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "FeeNoticeStage",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "FeesStartAtUtc",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "TransferFeeTier1MaxNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "TransferFeeTier1Naira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "TransferFeeTier2MaxNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "TransferFeeTier2Naira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "TransferFeeTier3Naira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "GatewayFeeNaira",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ServiceFeeNaira",
                table: "payments");
        }
    }
}
