using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    RoomNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PreferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreferenceValue = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AcknowledgedBy = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPreferences_AspNetUsers_AcknowledgedBy",
                        column: x => x.AcknowledgedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RoomPreferences_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RoomPreferences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomPreferenceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    RoomPreferenceId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedBy = table.Column<int>(type: "integer", nullable: true),
                    OldValue = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    NewValue = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPreferenceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPreferenceHistory_AspNetUsers_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RoomPreferenceHistory_RoomPreferences_RoomPreferenceId",
                        column: x => x.RoomPreferenceId,
                        principalTable: "RoomPreferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomPreferenceHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferenceHistory_ChangedBy",
                table: "RoomPreferenceHistory",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferenceHistory_RoomPreferenceId",
                table: "RoomPreferenceHistory",
                column: "RoomPreferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferenceHistory_TenantId_RoomPreferenceId",
                table: "RoomPreferenceHistory",
                columns: new[] { "TenantId", "RoomPreferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferences_AcknowledgedBy",
                table: "RoomPreferences",
                column: "AcknowledgedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferences_BookingId",
                table: "RoomPreferences",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferences_Status",
                table: "RoomPreferences",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferences_TenantId_BookingId_PreferenceType",
                table: "RoomPreferences",
                columns: new[] { "TenantId", "BookingId", "PreferenceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomPreferences_TenantId_RoomNumber",
                table: "RoomPreferences",
                columns: new[] { "TenantId", "RoomNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomPreferenceHistory");

            migrationBuilder.DropTable(
                name: "RoomPreferences");
        }
    }
}
