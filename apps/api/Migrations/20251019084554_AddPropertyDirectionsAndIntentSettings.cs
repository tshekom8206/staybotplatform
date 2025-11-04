using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyDirectionsAndIntentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    IntentName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnableUpselling = table.Column<bool>(type: "boolean", nullable: false),
                    CustomResponse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpsellStrategy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequiresStaffApproval = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyStaff = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedDepartment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaskPriority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AutoResolve = table.Column<bool>(type: "boolean", nullable: false),
                    AutoResolveDelayMinutes = table.Column<int>(type: "integer", nullable: true),
                    AdditionalConfig = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDirections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    FacilityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Directions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LocationDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Floor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Wing = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Landmarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Hours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdditionalInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDirections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDirections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntentSettings_TenantId",
                table: "IntentSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDirections_TenantId",
                table: "PropertyDirections",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntentSettings");

            migrationBuilder.DropTable(
                name: "PropertyDirections");
        }
    }
}
