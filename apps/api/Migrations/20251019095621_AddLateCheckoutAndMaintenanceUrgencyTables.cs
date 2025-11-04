using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLateCheckoutAndMaintenanceUrgencyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailablePlaceholders",
                table: "ResponseTemplates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentName",
                table: "ResponseTemplates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                table: "ResponseTemplates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyLevel",
                table: "ResponseTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualResolutionTime",
                table: "MaintenanceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EscalatedToManager",
                table: "MaintenanceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HabitabilityImpact",
                table: "MaintenanceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SafetyRisk",
                table: "MaintenanceRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetResolutionTime",
                table: "MaintenanceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyLevel",
                table: "MaintenanceRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalReason",
                table: "BookingModifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "BookingModifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "BookingModifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "BookingModifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "BookingModifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricingImpact",
                table: "BookingModifications",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RequestedCheckOutTime",
                table: "BookingModifications",
                type: "interval",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceUrgencyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeywordType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UrgencyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaskPriority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetMinutesToResolve = table.Column<int>(type: "integer", nullable: false),
                    IsSafetyRisk = table.Column<bool>(type: "boolean", nullable: false),
                    IsHabitabilityImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresManagerEscalation = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseTemplateId = table.Column<int>(type: "integer", nullable: true),
                    SafetyInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceUrgencyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceUrgencyRules_ResponseTemplates_ResponseTemplateId",
                        column: x => x.ResponseTemplateId,
                        principalTable: "ResponseTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceUrgencyRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    StandardCheckInTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    StandardCheckOutTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    LateCheckoutFeePerHour = table.Column<decimal>(type: "numeric", nullable: true),
                    EarlyCheckInFeePerHour = table.Column<decimal>(type: "numeric", nullable: true),
                    BusinessHoursStart = table.Column<TimeSpan>(type: "interval", nullable: true),
                    BusinessHoursEnd = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_ApprovedByUserId",
                table: "BookingModifications",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceUrgencyRules_ResponseTemplateId",
                table: "MaintenanceUrgencyRules",
                column: "ResponseTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceUrgencyRules_TenantId",
                table: "MaintenanceUrgencyRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId",
                table: "TenantSettings",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingModifications_AspNetUsers_ApprovedByUserId",
                table: "BookingModifications",
                column: "ApprovedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingModifications_AspNetUsers_ApprovedByUserId",
                table: "BookingModifications");

            migrationBuilder.DropTable(
                name: "MaintenanceUrgencyRules");

            migrationBuilder.DropTable(
                name: "TenantSettings");

            migrationBuilder.DropIndex(
                name: "IX_BookingModifications_ApprovedByUserId",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "AvailablePlaceholders",
                table: "ResponseTemplates");

            migrationBuilder.DropColumn(
                name: "IntentName",
                table: "ResponseTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                table: "ResponseTemplates");

            migrationBuilder.DropColumn(
                name: "UrgencyLevel",
                table: "ResponseTemplates");

            migrationBuilder.DropColumn(
                name: "ActualResolutionTime",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "EscalatedToManager",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "HabitabilityImpact",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "SafetyRisk",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "TargetResolutionTime",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "UrgencyLevel",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalReason",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "PricingImpact",
                table: "BookingModifications");

            migrationBuilder.DropColumn(
                name: "RequestedCheckOutTime",
                table: "BookingModifications");
        }
    }
}
