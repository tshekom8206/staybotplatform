using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPushSubscriptionGuestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "PushSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "PushSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "PushSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "PushSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_BookingId",
                table: "PushSubscriptions",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_PushSubscriptions_Bookings_BookingId",
                table: "PushSubscriptions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PushSubscriptions_Bookings_BookingId",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_BookingId",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "PushSubscriptions");
        }
    }
}
