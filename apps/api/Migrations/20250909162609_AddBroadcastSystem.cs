﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BroadcastMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EstimatedRestorationTime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalRecipients = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulDeliveries = table.Column<int>(type: "integer", nullable: false),
                    FailedDeliveries = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BroadcastMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BroadcastMessageId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BroadcastRecipients_BroadcastMessages_BroadcastMessageId",
                        column: x => x.BroadcastMessageId,
                        principalTable: "BroadcastMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BroadcastRecipients_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastMessages_TenantId",
                table: "BroadcastMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_BroadcastMessageId",
                table: "BroadcastRecipients",
                column: "BroadcastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_ConversationId",
                table: "BroadcastRecipients",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastRecipients");

            migrationBuilder.DropTable(
                name: "BroadcastMessages");
        }
    }
}
