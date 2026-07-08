using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servika.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConversationsRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chat moved from per-booking to per-(customer, artisan) conversations.
            // The old rows are keyed by BookingId (reused here as ConversationId),
            // which no longer resolves to a conversation, so clear them — this is
            // disposable dev/test chat with no conversations table to back it yet.
            migrationBuilder.Sql("DELETE FROM chat_messages;");

            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_bookings_BookingId",
                table: "chat_messages");

            migrationBuilder.RenameColumn(
                name: "BookingId",
                table: "chat_messages",
                newName: "ConversationId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_messages_BookingId_CreatedAt",
                table: "chat_messages",
                newName: "IX_chat_messages_ConversationId_CreatedAt");

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtisanUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversations_users_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_ArtisanUserId",
                table: "conversations",
                column: "ArtisanUserId");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CustomerUserId_ArtisanId",
                table: "conversations",
                columns: new[] { "CustomerUserId", "ArtisanId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_conversations_ConversationId",
                table: "chat_messages",
                column: "ConversationId",
                principalTable: "conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_conversations_ConversationId",
                table: "chat_messages");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.RenameColumn(
                name: "ConversationId",
                table: "chat_messages",
                newName: "BookingId");

            migrationBuilder.RenameIndex(
                name: "IX_chat_messages_ConversationId_CreatedAt",
                table: "chat_messages",
                newName: "IX_chat_messages_BookingId_CreatedAt");

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "electrical" }, new List<string> { "electrician", "hvac", "fridge", "carpenter" }, new List<string> { "Installation", "Repair", "Maintenance", "Wiring" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000002"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "plumbing" }, new List<string> { "plumber", "fridge", "electrician", "carpenter" }, new List<string> { "Leak Repair", "Installation", "Drainage", "Maintenance" } });

            migrationBuilder.UpdateData(
                table: "artisan_profiles",
                keyColumn: "Id",
                keyValue: new Guid("b0000000-0000-0000-0000-000000000003"),
                columns: new[] { "CategorySlugs", "GalleryKeys", "Services" },
                values: new object[] { new List<string> { "ac", "fridge" }, new List<string> { "hvac", "fridge", "electrician", "plumber" }, new List<string> { "AC Servicing", "Installation", "Gas Refill", "Repair" } });

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_bookings_BookingId",
                table: "chat_messages",
                column: "BookingId",
                principalTable: "bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
