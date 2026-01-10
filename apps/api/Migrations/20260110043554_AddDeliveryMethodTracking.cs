using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryMethodTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the three new columns
            migrationBuilder.AddColumn<int>(
                name: "AttemptedMethod",
                table: "ScheduledMessages",
                type: "integer",
                nullable: false,
                defaultValue: 2); // Default to WhatsApp (DeliveryMethod.WhatsApp)

            migrationBuilder.AddColumn<int>(
                name: "SuccessfulMethod",
                table: "ScheduledMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppFailureReason",
                table: "ScheduledMessages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Migrate existing sent messages to have SMS as their delivery method
            migrationBuilder.Sql(@"
                UPDATE ""ScheduledMessages""
                SET ""AttemptedMethod"" = 1,
                    ""SuccessfulMethod"" = CASE WHEN ""Status"" = 1 THEN 1 ELSE NULL END
                WHERE ""Status"" IN (1, 2);
            ");

            // Add indexes for cost analysis queries
            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_SuccessfulMethod_SentAt",
                table: "ScheduledMessages",
                columns: new[] { "SuccessfulMethod", "SentAt" },
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_TenantId_SuccessfulMethod_SentAt",
                table: "ScheduledMessages",
                columns: new[] { "TenantId", "SuccessfulMethod", "SentAt" },
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_ScheduledMessages_SuccessfulMethod_SentAt",
                table: "ScheduledMessages");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledMessages_TenantId_SuccessfulMethod_SentAt",
                table: "ScheduledMessages");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "AttemptedMethod",
                table: "ScheduledMessages");

            migrationBuilder.DropColumn(
                name: "SuccessfulMethod",
                table: "ScheduledMessages");

            migrationBuilder.DropColumn(
                name: "WhatsAppFailureReason",
                table: "ScheduledMessages");
        }
    }
}
