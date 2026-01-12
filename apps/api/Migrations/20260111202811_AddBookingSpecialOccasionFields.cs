using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSpecialOccasionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnniversary",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBirthday",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpecialOccasionNotes",
                table: "Bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnniversary",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsBirthday",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SpecialOccasionNotes",
                table: "Bookings");
        }
    }
}
