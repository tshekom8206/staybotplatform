using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "PostStaySurveys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "PostStaySurveys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "PostStaySurveys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SentSuccessfully",
                table: "PostStaySurveys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInDate",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutDate",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtendedFromBookingId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatGuest",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStaff",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreviousBookingId",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomRate",
                table: "Bookings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SurveyOptOut",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalNights",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRevenue",
                table: "Bookings",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuestBusinessMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstStayDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStayDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalStays = table.Column<int>(type: "integer", nullable: false),
                    LifetimeValue = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageSatisfaction = table.Column<decimal>(type: "numeric", nullable: true),
                    DaysSinceLastStay = table.Column<int>(type: "integer", nullable: false),
                    HasReferred = table.Column<bool>(type: "boolean", nullable: false),
                    WillReturn = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestBusinessMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestBusinessMetrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_SurveyToken",
                table: "PostStaySurveys",
                column: "SurveyToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_TenantId_CompletedAt",
                table: "PostStaySurveys",
                columns: new[] { "TenantId", "CompletedAt" },
                filter: "\"CompletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_TenantId_IsCompleted",
                table: "PostStaySurveys",
                columns: new[] { "TenantId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_TenantId_SentAt",
                table: "PostStaySurveys",
                columns: new[] { "TenantId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_TenantId_SentSuccessfully",
                table: "PostStaySurveys",
                columns: new[] { "TenantId", "SentSuccessfully" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_TenantId_CollectionMethod",
                table: "GuestRatings",
                columns: new[] { "TenantId", "CollectionMethod" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_TenantId_CreatedAt",
                table: "GuestRatings",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_TenantId_Department",
                table: "GuestRatings",
                columns: new[] { "TenantId", "Department" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_TenantId_Rating",
                table: "GuestRatings",
                columns: new[] { "TenantId", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ExtendedFromBookingId",
                table: "Bookings",
                column: "ExtendedFromBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PreviousBookingId",
                table: "Bookings",
                column: "PreviousBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestBusinessMetrics_TenantId_AverageSatisfaction",
                table: "GuestBusinessMetrics",
                columns: new[] { "TenantId", "AverageSatisfaction" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestBusinessMetrics_TenantId_LifetimeValue",
                table: "GuestBusinessMetrics",
                columns: new[] { "TenantId", "LifetimeValue" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestBusinessMetrics_TenantId_PhoneNumber",
                table: "GuestBusinessMetrics",
                columns: new[] { "TenantId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestBusinessMetrics_TenantId_TotalStays",
                table: "GuestBusinessMetrics",
                columns: new[] { "TenantId", "TotalStays" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Bookings_ExtendedFromBookingId",
                table: "Bookings",
                column: "ExtendedFromBookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Bookings_PreviousBookingId",
                table: "Bookings",
                column: "PreviousBookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Bookings_ExtendedFromBookingId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Bookings_PreviousBookingId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "GuestBusinessMetrics");

            migrationBuilder.DropIndex(
                name: "IX_PostStaySurveys_SurveyToken",
                table: "PostStaySurveys");

            migrationBuilder.DropIndex(
                name: "IX_PostStaySurveys_TenantId_CompletedAt",
                table: "PostStaySurveys");

            migrationBuilder.DropIndex(
                name: "IX_PostStaySurveys_TenantId_IsCompleted",
                table: "PostStaySurveys");

            migrationBuilder.DropIndex(
                name: "IX_PostStaySurveys_TenantId_SentAt",
                table: "PostStaySurveys");

            migrationBuilder.DropIndex(
                name: "IX_PostStaySurveys_TenantId_SentSuccessfully",
                table: "PostStaySurveys");

            migrationBuilder.DropIndex(
                name: "IX_GuestRatings_TenantId_CollectionMethod",
                table: "GuestRatings");

            migrationBuilder.DropIndex(
                name: "IX_GuestRatings_TenantId_CreatedAt",
                table: "GuestRatings");

            migrationBuilder.DropIndex(
                name: "IX_GuestRatings_TenantId_Department",
                table: "GuestRatings");

            migrationBuilder.DropIndex(
                name: "IX_GuestRatings_TenantId_Rating",
                table: "GuestRatings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ExtendedFromBookingId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PreviousBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "PostStaySurveys");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "PostStaySurveys");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "PostStaySurveys");

            migrationBuilder.DropColumn(
                name: "SentSuccessfully",
                table: "PostStaySurveys");

            migrationBuilder.DropColumn(
                name: "CheckInDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CheckOutDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExtendedFromBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsRepeatGuest",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsStaff",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PreviousBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RoomRate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SurveyOptOut",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TotalNights",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TotalRevenue",
                table: "Bookings");
        }
    }
}
