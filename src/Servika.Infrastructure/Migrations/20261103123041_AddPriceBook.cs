using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiagnosisFeeNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 5000);

            migrationBuilder.AddColumn<decimal>(
                name: "QuoteReviewRangeFactor",
                table: "platform_settings",
                type: "numeric",
                nullable: false,
                defaultValue: 1.5m);

            migrationBuilder.AddColumn<int>(
                name: "QuoteReviewThresholdNaira",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 50000);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceBookItemId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_book_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Scope = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PriceNaira = table.Column<int>(type: "integer", nullable: true),
                    RangeMinNaira = table.Column<int>(type: "integer", nullable: true),
                    RangeMaxNaira = table.Column<int>(type: "integer", nullable: true),
                    PartsNote = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PhotoKey = table.Column<string>(type: "text", nullable: true),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPlaceholderPrice = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_book_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_PriceBookItemId",
                table: "bookings",
                column: "PriceBookItemId");

            migrationBuilder.CreateIndex(
                name: "IX_price_book_items_CategorySlug_Kind_SortOrder",
                table: "price_book_items",
                columns: new[] { "CategorySlug", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_price_book_items_IsPopular",
                table: "price_book_items",
                column: "IsPopular");

            // Starter price book: the samples from Business Model v1.4 section 7, marked as placeholders.
            // They must be replaced with prices from real phone quotes in the console before launch.
            var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "price_book_items",
                columns: new[] { "Id", "CategorySlug", "Kind", "Name", "Scope", "PriceNaira", "RangeMinNaira", "RangeMaxNaira", "PartsNote", "DurationMinutes", "IsPopular", "IsActive", "SortOrder", "IsPlaceholderPrice", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("3e8cd261-6bb6-5c81-914f-8a281126bacb"), "ac", "Fixed", "AC servicing", "Split unit, 1–1.5HP: wash coils and filters, check drain and gas pressure", 15000, null, null, null, 90, true, true, 1, true, now, now },
                    { new Guid("d34b8db8-542b-5f5a-84cf-9c88f142b5db"), "ac", "Fixed", "Extra AC unit, same visit", "Each extra split unit serviced on the same visit", 10000, null, null, null, 60, false, true, 2, true, now, now },
                    { new Guid("31d16254-70ff-5a62-b6ef-8f6b8d565524"), "ac", "Fixed", "AC gas top-up", "Partial gas top-up on a split unit that is cooling poorly, gas included", 20000, null, null, "Gas included", 60, true, true, 3, true, now, now },
                    { new Guid("e176cca6-69c5-50ba-acac-e824c7fee29a"), "generator", "Fixed", "Generator servicing", "Petrol generator up to 5kVA: oil change, plug, air filter, carburettor clean", 12000, null, null, "Oil and plug at listed parts price", 90, true, true, 1, true, now, now },
                    { new Guid("adf08738-0b08-530c-9cf2-d703f0335332"), "solar", "Fixed", "Inverter and battery check", "System health check: batteries, inverter settings, connections, panel inspection", 15000, null, null, null, 60, true, true, 1, true, now, now },
                    { new Guid("5e520ad3-5ef9-59c9-a9a4-ded7188c76dc"), "solar", "Fixed", "Solar panel cleaning", "Up to 8 panels, roof access permitting", 10000, null, null, null, 60, false, true, 2, true, now, now },
                    { new Guid("b0160953-eb76-5d58-8ee3-7fa945738433"), "electrical", "Fixed", "Socket or switch replacement", "Replace sockets or switches, up to 3 points", 8000, null, null, "Customer-supplied or listed parts", 60, true, true, 1, true, now, now },
                    { new Guid("6ea5b293-a058-5514-8b2e-99f455f19366"), "electrical", "Fixed", "Ceiling fan installation", "Fit and connect one ceiling fan", 10000, null, null, "Fan supplied by you", 60, false, true, 2, true, now, now },
                    { new Guid("a3bfc985-2898-5202-9521-6e7700c2f046"), "plumbing", "Fixed", "Tap or mixer replacement", "Replace taps or mixers, up to 2", 8000, null, null, "Customer-supplied or listed parts", 60, false, true, 1, true, now, now },
                    { new Guid("e286b107-c514-55ee-aad0-eabb096d5d00"), "plumbing", "Fixed", "WC cistern repair", "Fix a running or non-flushing toilet cistern", 10000, null, null, "Parts at listed price", 60, false, true, 2, true, now, now },
                    { new Guid("9b763e46-6c2a-5dea-9124-4775d42a2a42"), "plumbing", "Fixed", "Overhead tank cleaning", "Drain, scrub and rinse a tank of up to 2,000 litres", 20000, null, null, null, 180, true, true, 3, true, now, now },
                    { new Guid("de9c0e9e-94a1-562f-8cf7-2532c2585aec"), "ac", "AddOn", "Replace capacitor", "Swap a failed compressor or fan capacitor", 8000, null, null, "Plus listed part", null, false, true, 10, true, now, now },
                    { new Guid("f34966c3-bd39-5a3b-8888-84b6c2da9255"), "ac", "AddOn", "Gas top-up (partial)", "Top up refrigerant on a unit with no leak", 20000, null, null, "Gas included", null, false, true, 11, true, now, now },
                    { new Guid("564c14e2-e7a7-568c-afc3-a056355ecb82"), "ac", "AddOn", "Full gas recharge after leak repair", "Find and seal the leak, then recharge fully", 35000, null, null, "Gas included", null, false, true, 12, true, now, now },
                    { new Guid("b82591df-8b72-5036-800b-764f55da5004"), "ac", "AddOn", "Unblock drain line", "Clear a blocked condensate drain", 6000, null, null, null, null, false, true, 13, true, now, now },
                    { new Guid("bda9bacd-ab68-5bdd-a066-d67f38f466c3"), "generator", "AddOn", "Replace AVR", "Fit a new voltage regulator", 8000, null, null, "AVR at listed price", null, false, true, 10, true, now, now },
                    { new Guid("ab78c5ec-cc52-5dbd-bb55-1c7524df8bf1"), "generator", "AddOn", "Carburettor overhaul", "Strip, clean and rebuild the carburettor", 8000, null, null, "Plus listed kit", null, false, true, 11, true, now, now },
                    { new Guid("a09bf68f-58af-57b4-800b-1bbd1ff09044"), "electrical", "AddOn", "Trace and fix fault, per circuit", "Find and repair a fault on one circuit", 8000, null, null, null, null, false, true, 10, true, now, now },
                    { new Guid("3eaad0ff-5321-5532-8998-278b0bb19479"), "electrical", "AddOn", "Replace breaker", "Swap a faulty breaker in the board", 6000, null, null, "Plus listed breaker", null, false, true, 11, true, now, now },
                    { new Guid("9cb7ce2a-f0ab-5407-a080-22abf7136e1b"), "plumbing", "AddOn", "Replace pump capacitor or pressure switch", "Borehole or surface pump part swap", 8000, null, null, "Plus listed part", null, false, true, 10, true, now, now },
                    { new Guid("762ba889-da06-5690-9ed2-0f63564fd3fa"), "plumbing", "AddOn", "Repair exposed pipe leak", "Up to 1 metre of exposed pipe", 10000, null, null, "Fittings at listed price", null, false, true, 11, true, now, now },
                    { new Guid("5ed6461b-3d24-5ead-9251-33cc838b9e55"), "ac", "Custom", "New split AC installation", "1–1.5HP, up to 3 metres of piping", null, 25000, 60000, "Piping, bracket, cable at listed prices", null, false, true, 20, true, now, now },
                    { new Guid("80b9f5ee-b0ae-5407-9a6e-a7a653d468a0"), "plumbing", "Custom", "Submersible pump replacement", "Pull the old pump, fit and test the new one", null, 30000, 60000, "Pump itemised", null, false, true, 20, true, now, now },
                    { new Guid("6153a70d-a6f1-5fae-8e28-1e40f29bddd7"), "solar", "Custom", "Inverter and battery installation", "3.5kVA system", null, 40000, 80000, "Equipment supplied by you or Servika", null, false, true, 20, true, now, now },
                    { new Guid("5fb3971e-e399-5b29-9370-4c1b7393ccc4"), "electrical", "Custom", "Rewire one room", "New cable and fittings for one room", null, 60000, 150000, "Cable and fittings itemised", null, false, true, 20, true, now, now },
                    { new Guid("0640875d-f96a-5116-a258-c782300af126"), "security", "Custom", "CCTV installation, 4 cameras", "Mount, cable and set up 4 cameras and the recorder", null, 40000, 80000, "Kit itemised", null, false, true, 20, true, now, now },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_book_items");

            migrationBuilder.DropIndex(
                name: "IX_bookings_PriceBookItemId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DiagnosisFeeNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "QuoteReviewRangeFactor",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "QuoteReviewThresholdNaira",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "PriceBookItemId",
                table: "bookings");
        }
    }
}
