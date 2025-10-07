using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTaskModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "StaffTasks_RequestItemId_fkey",
                table: "StaffTasks");

            migrationBuilder.AlterColumn<int>(
                name: "RequestItemId",
                table: "StaffTasks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "AssignedToId",
                table: "StaffTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "StaffTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedBy",
                table: "StaffTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "StaffTasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StaffTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedCompletionTime",
                table: "StaffTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "StaffTasks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "StaffTasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "StaffTasks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StaffTasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "RequestItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "RequestItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedTime",
                table: "RequestItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUrgent",
                table: "RequestItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "LostAndFoundCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_AssignedToId",
                table: "StaffTasks",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_BookingId",
                table: "StaffTasks",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_CompletedBy",
                table: "StaffTasks",
                column: "CompletedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffTasks_AspNetUsers_AssignedToId",
                table: "StaffTasks",
                column: "AssignedToId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffTasks_AspNetUsers_CompletedBy",
                table: "StaffTasks",
                column: "CompletedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffTasks_Bookings_BookingId",
                table: "StaffTasks",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "StaffTasks_RequestItemId_fkey",
                table: "StaffTasks",
                column: "RequestItemId",
                principalTable: "RequestItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffTasks_AspNetUsers_AssignedToId",
                table: "StaffTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTasks_AspNetUsers_CompletedBy",
                table: "StaffTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTasks_Bookings_BookingId",
                table: "StaffTasks");

            migrationBuilder.DropForeignKey(
                name: "StaffTasks_RequestItemId_fkey",
                table: "StaffTasks");

            migrationBuilder.DropIndex(
                name: "IX_StaffTasks_AssignedToId",
                table: "StaffTasks");

            migrationBuilder.DropIndex(
                name: "IX_StaffTasks_BookingId",
                table: "StaffTasks");

            migrationBuilder.DropIndex(
                name: "IX_StaffTasks_CompletedBy",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "EstimatedCompletionTime",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "EstimatedTime",
                table: "RequestItems");

            migrationBuilder.DropColumn(
                name: "IsUrgent",
                table: "RequestItems");

            migrationBuilder.AlterColumn<int>(
                name: "RequestItemId",
                table: "StaffTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string[]>(
                name: "Keywords",
                table: "LostAndFoundCategories",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "StaffTasks_RequestItemId_fkey",
                table: "StaffTasks",
                column: "RequestItemId",
                principalTable: "RequestItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
