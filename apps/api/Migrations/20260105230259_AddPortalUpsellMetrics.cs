using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalUpsellMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalUpsellMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServicePrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ServiceCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoomNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    StaffTaskId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalUpsellMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalUpsellMetrics_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortalUpsellMetrics_StaffTasks_StaffTaskId",
                        column: x => x.StaffTaskId,
                        principalTable: "StaffTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PortalUpsellMetrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalUpsellMetrics_ServiceId",
                table: "PortalUpsellMetrics",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalUpsellMetrics_StaffTaskId",
                table: "PortalUpsellMetrics",
                column: "StaffTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalUpsellMetrics_TenantId",
                table: "PortalUpsellMetrics",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalUpsellMetrics");
        }
    }
}
