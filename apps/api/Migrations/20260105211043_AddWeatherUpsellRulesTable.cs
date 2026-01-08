using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherUpsellRulesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherUpsellRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    WeatherCondition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MinTemperature = table.Column<int>(type: "integer", nullable: true),
                    MaxTemperature = table.Column<int>(type: "integer", nullable: true),
                    WeatherCodes = table.Column<string>(type: "text", nullable: true),
                    ServiceIds = table.Column<string>(type: "text", nullable: false),
                    BannerText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BannerIcon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherUpsellRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherUpsellRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherUpsellRules_TenantId_IsActive_Priority",
                table: "WeatherUpsellRules",
                columns: new[] { "TenantId", "IsActive", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherUpsellRules");
        }
    }
}
