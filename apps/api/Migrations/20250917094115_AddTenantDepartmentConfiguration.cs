using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDepartmentConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ContactInfo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorkingHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaxConcurrentTasks = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDepartments", x => x.Id);
                    table.UniqueConstraint("AK_TenantDepartments_DepartmentName", x => x.DepartmentName);
                    table.ForeignKey(
                        name: "FK_TenantDepartments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDepartmentMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ServiceCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetDepartment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiresRoomDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAdvanceBooking = table.Column<bool>(type: "boolean", nullable: false),
                    ContactMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SpecialInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDepartmentMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDepartmentMappings_TenantDepartments_TargetDepartment",
                        column: x => x.TargetDepartment,
                        principalTable: "TenantDepartments",
                        principalColumn: "DepartmentName",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDepartmentMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDepartmentMappings_TargetDepartment",
                table: "ServiceDepartmentMappings",
                column: "TargetDepartment");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDepartmentMappings_TenantId_ServiceCategory",
                table: "ServiceDepartmentMappings",
                columns: new[] { "TenantId", "ServiceCategory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantDepartments_TenantId_DepartmentName",
                table: "TenantDepartments",
                columns: new[] { "TenantId", "DepartmentName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDepartmentMappings");

            migrationBuilder.DropTable(
                name: "TenantDepartments");
        }
    }
}
