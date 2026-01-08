using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelLocationCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "HotelInfos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "HotelInfos",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "HotelInfos");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "HotelInfos");
        }
    }
}
