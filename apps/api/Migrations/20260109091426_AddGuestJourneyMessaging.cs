using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestJourneyMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckinDayTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MidStayTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostStayTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreArrivalDaysBefore",
                table: "ProactiveMessageSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PreArrivalEnabled",
                table: "ProactiveMessageSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreArrivalTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PreArrivalTime",
                table: "ProactiveMessageSettings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "PreCheckoutTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WelcomeSettledEnabled",
                table: "ProactiveMessageSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WelcomeSettledHoursAfter",
                table: "ProactiveMessageSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeSettledTemplate",
                table: "ProactiveMessageSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckinDayTemplate",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "MidStayTemplate",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PostStayTemplate",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PreArrivalDaysBefore",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PreArrivalEnabled",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PreArrivalTemplate",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PreArrivalTime",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "PreCheckoutTemplate",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "WelcomeSettledEnabled",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "WelcomeSettledHoursAfter",
                table: "ProactiveMessageSettings");

            migrationBuilder.DropColumn(
                name: "WelcomeSettledTemplate",
                table: "ProactiveMessageSettings");
        }
    }
}
