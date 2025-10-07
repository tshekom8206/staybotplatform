using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUpsellMetricsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpsellMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    SuggestedServiceId = table.Column<int>(type: "integer", nullable: false),
                    SuggestedServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SuggestedServicePrice = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    SuggestedServiceCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TriggerContext = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TriggerServiceId = table.Column<int>(type: "integer", nullable: true),
                    WasSuggested = table.Column<bool>(type: "boolean", nullable: false),
                    WasAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedVia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revenue = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    SuggestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpsellMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpsellMetrics_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UpsellMetrics_Services_SuggestedServiceId",
                        column: x => x.SuggestedServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UpsellMetrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpsellMetrics_ConversationId",
                table: "UpsellMetrics",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_UpsellMetrics_SuggestedServiceId",
                table: "UpsellMetrics",
                column: "SuggestedServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_UpsellMetrics_TenantId",
                table: "UpsellMetrics",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpsellMetrics");
        }
    }
}
