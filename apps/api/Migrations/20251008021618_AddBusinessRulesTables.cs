using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessRulesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequestItemRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    RequestItemId = table.Column<int>(type: "integer", nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ValidationMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MaxPerRoom = table.Column<int>(type: "integer", nullable: true),
                    MaxPerGuest = table.Column<int>(type: "integer", nullable: true),
                    RequiresActiveBooking = table.Column<bool>(type: "boolean", nullable: false),
                    RestrictedHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpsellSuggestions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelevanceContext = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MinConfidenceScore = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestItemRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestItemRules_RequestItems_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "RequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestItemRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceBusinessRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ValidationMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpsellSuggestions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelevanceContext = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MinConfidenceScore = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceBusinessRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceBusinessRules_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceBusinessRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestItemRules_Item_Active",
                table: "RequestItemRules",
                columns: new[] { "RequestItemId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestItemRules_RuleType",
                table: "RequestItemRules",
                column: "RuleType");

            migrationBuilder.CreateIndex(
                name: "IX_RequestItemRules_Tenant",
                table: "RequestItemRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBusinessRules_RuleType",
                table: "ServiceBusinessRules",
                column: "RuleType");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBusinessRules_Service_Active",
                table: "ServiceBusinessRules",
                columns: new[] { "ServiceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceBusinessRules_Tenant",
                table: "ServiceBusinessRules",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestItemRules");

            migrationBuilder.DropTable(
                name: "ServiceBusinessRules");
        }
    }
}
