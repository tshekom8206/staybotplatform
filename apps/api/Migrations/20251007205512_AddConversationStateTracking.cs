using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationStateTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingInfoState",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationMode",
                table: "Conversations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastBotAction",
                table: "Conversations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingInfoState",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ConversationMode",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastBotAction",
                table: "Conversations");
        }
    }
}
