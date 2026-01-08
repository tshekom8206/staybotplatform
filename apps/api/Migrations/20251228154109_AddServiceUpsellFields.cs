using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceUpsellFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tenant columns already exist in database, only add Service upselling columns
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedImageUrl",
                table: "Services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TimeSlots",
                table: "Services",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only drop Service upselling columns (Tenant columns were already present)
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "FeaturedImageUrl",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "TimeSlots",
                table: "Services");
        }
    }
}
