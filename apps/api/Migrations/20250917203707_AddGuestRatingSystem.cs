using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRatingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: true),
                    TaskId = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    RatingType = table.Column<string>(type: "text", nullable: false),
                    GuestName = table.Column<string>(type: "text", nullable: true),
                    GuestPhone = table.Column<string>(type: "text", nullable: true),
                    RoomNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectionMethod = table.Column<string>(type: "text", nullable: false),
                    WouldRecommend = table.Column<bool>(type: "boolean", nullable: false),
                    NpsScore = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestRatings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestRatings_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GuestRatings_StaffTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "StaffTasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PostStaySurveys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    GuestName = table.Column<string>(type: "text", nullable: false),
                    GuestEmail = table.Column<string>(type: "text", nullable: false),
                    GuestPhone = table.Column<string>(type: "text", nullable: true),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    CleanlinessRating = table.Column<int>(type: "integer", nullable: false),
                    ServiceRating = table.Column<int>(type: "integer", nullable: false),
                    AmenitiesRating = table.Column<int>(type: "integer", nullable: false),
                    ValueRating = table.Column<int>(type: "integer", nullable: false),
                    FrontDeskRating = table.Column<int>(type: "integer", nullable: true),
                    HousekeepingRating = table.Column<int>(type: "integer", nullable: true),
                    MaintenanceRating = table.Column<int>(type: "integer", nullable: true),
                    FoodServiceRating = table.Column<int>(type: "integer", nullable: true),
                    WhatWentWell = table.Column<string>(type: "text", nullable: true),
                    WhatCouldImprove = table.Column<string>(type: "text", nullable: true),
                    AdditionalComments = table.Column<string>(type: "text", nullable: true),
                    NpsScore = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SurveyToken = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostStaySurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostStaySurveys_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_BookingId",
                table: "GuestRatings",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_ConversationId",
                table: "GuestRatings",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestRatings_TaskId",
                table: "GuestRatings",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_PostStaySurveys_BookingId",
                table: "PostStaySurveys",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestRatings");

            migrationBuilder.DropTable(
                name: "PostStaySurveys");
        }
    }
}
