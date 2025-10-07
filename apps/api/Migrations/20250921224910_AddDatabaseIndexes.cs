using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add performance indexes for most queried columns
            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_Direction_CreatedAt",
                table: "Messages",
                columns: new[] { "ConversationId", "Direction", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_CreatedAt",
                table: "Messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Services_TenantId_Name",
                table: "Services",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_TenantId_CreatedAt",
                table: "Conversations",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WaUserPhone_TenantId",
                table: "Conversations",
                columns: new[] { "WaUserPhone", "TenantId" });

            // Note: Skipping MenuItems and RequestItems indexes for now due to column schema differences
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
