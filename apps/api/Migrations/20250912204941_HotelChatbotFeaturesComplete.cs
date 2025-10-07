using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class HotelChatbotFeaturesComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmergencyIncidentId",
                table: "StaffTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceRequestId",
                table: "StaffTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "RequestItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingModifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: true),
                    ModificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestDetails = table.Column<string>(type: "text", nullable: false),
                    OriginalCheckinDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalCheckoutDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalGuestName = table.Column<string>(type: "text", nullable: true),
                    OriginalPhone = table.Column<string>(type: "text", nullable: true),
                    NewCheckinDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewCheckoutDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewGuestName = table.Column<string>(type: "text", nullable: true),
                    NewPhone = table.Column<string>(type: "text", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FeeDifference = table.Column<decimal>(type: "numeric", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    StaffNotes = table.Column<string>(type: "text", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingModifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingModifications_AspNetUsers_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingModifications_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingModifications_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingModifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetectionKeywords = table.Column<string>(type: "text", nullable: false),
                    SeverityLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoEscalate = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresEvacuation = table.Column<bool>(type: "boolean", nullable: false),
                    ContactEmergencyServices = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LocationFound = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FinderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FoundAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StorageLocation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StorageNotes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisposalAfterDays = table.Column<int>(type: "integer", nullable: false),
                    AdditionalDetails = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostAndFoundCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    DefaultDisposalDays = table.Column<int>(type: "integer", nullable: false),
                    RequiresSecureStorage = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostAndFoundCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostAndFoundCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LocationLost = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReporterPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReporterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConversationId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    SpecialInstructions = table.Column<string>(type: "text", nullable: true),
                    AdditionalDetails = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostItems_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LostItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModelNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextScheduledService = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServiceIntervalDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingChangeHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    BookingModificationId = table.Column<int>(type: "integer", nullable: true),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingChangeHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingChangeHistory_BookingModifications_BookingModificati~",
                        column: x => x.BookingModificationId,
                        principalTable: "BookingModifications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingChangeHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingChangeHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EmergencyTypeId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SeverityLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AffectedAreas = table.Column<string>(type: "text", nullable: false),
                    ResponseActions = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_EmergencyTypes_EmergencyTypeId",
                        column: x => x.EmergencyTypeId,
                        principalTable: "EmergencyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyIncidents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyProtocols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EmergencyTypeId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcedureSteps = table.Column<string>(type: "text", nullable: false),
                    TriggerCondition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NotifyGuests = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyStaff = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyEmergencyServices = table.Column<bool>(type: "boolean", nullable: false),
                    GuestMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StaffMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmergencyContacts = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyProtocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyProtocols_EmergencyTypes_EmergencyTypeId",
                        column: x => x.EmergencyTypeId,
                        principalTable: "EmergencyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmergencyProtocols_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostAndFoundMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    LostItemId = table.Column<int>(type: "integer", nullable: false),
                    FoundItemId = table.Column<int>(type: "integer", nullable: false),
                    MatchScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchingReason = table.Column<string>(type: "text", nullable: true),
                    VerifiedBy = table.Column<int>(type: "integer", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GuestConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    GuestConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostAndFoundMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostAndFoundMatches_AspNetUsers_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LostAndFoundMatches_FoundItems_FoundItemId",
                        column: x => x.FoundItemId,
                        principalTable: "FoundItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LostAndFoundMatches_LostItems_LostItemId",
                        column: x => x.LostItemId,
                        principalTable: "LostItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LostAndFoundMatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceItemId = table.Column<int>(type: "integer", nullable: true),
                    ConversationId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportedBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AssignedTo = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_AspNetUsers_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_MaintenanceItems_MaintenanceItemId",
                        column: x => x.MaintenanceItemId,
                        principalTable: "MaintenanceItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostAndFoundNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LostItemId = table.Column<int>(type: "integer", nullable: true),
                    FoundItemId = table.Column<int>(type: "integer", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: true),
                    RecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostAndFoundNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostAndFoundNotifications_FoundItems_FoundItemId",
                        column: x => x.FoundItemId,
                        principalTable: "FoundItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LostAndFoundNotifications_LostAndFoundMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "LostAndFoundMatches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LostAndFoundNotifications_LostItems_LostItemId",
                        column: x => x.LostItemId,
                        principalTable: "LostItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LostAndFoundNotifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceItemId = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceRequestId = table.Column<int>(type: "integer", nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true),
                    PartsReplaced = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    NextServiceDue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceHistory_MaintenanceItems_MaintenanceItemId",
                        column: x => x.MaintenanceItemId,
                        principalTable: "MaintenanceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceHistory_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_EmergencyIncidentId",
                table: "StaffTasks",
                column: "EmergencyIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffTasks_MaintenanceRequestId",
                table: "StaffTasks",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeHistory_BookingId",
                table: "BookingChangeHistory",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeHistory_BookingModificationId",
                table: "BookingChangeHistory",
                column: "BookingModificationId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingChangeHistory_TenantId",
                table: "BookingChangeHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_BookingId",
                table: "BookingModifications",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_ConversationId",
                table: "BookingModifications",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_ProcessedBy",
                table: "BookingModifications",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BookingModifications_TenantId",
                table: "BookingModifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContacts_TenantId",
                table: "EmergencyContacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_ConversationId",
                table: "EmergencyIncidents",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_EmergencyTypeId",
                table: "EmergencyIncidents",
                column: "EmergencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyIncidents_TenantId",
                table: "EmergencyIncidents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProtocols_EmergencyTypeId",
                table: "EmergencyProtocols",
                column: "EmergencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProtocols_TenantId",
                table: "EmergencyProtocols",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyTypes_TenantId",
                table: "EmergencyTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundItems_TenantId",
                table: "FoundItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundCategories_TenantId",
                table: "LostAndFoundCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundMatches_FoundItemId",
                table: "LostAndFoundMatches",
                column: "FoundItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundMatches_LostItemId",
                table: "LostAndFoundMatches",
                column: "LostItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundMatches_TenantId",
                table: "LostAndFoundMatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundMatches_VerifiedBy",
                table: "LostAndFoundMatches",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundNotifications_FoundItemId",
                table: "LostAndFoundNotifications",
                column: "FoundItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundNotifications_LostItemId",
                table: "LostAndFoundNotifications",
                column: "LostItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundNotifications_MatchId",
                table: "LostAndFoundNotifications",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LostAndFoundNotifications_TenantId",
                table: "LostAndFoundNotifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LostItems_ConversationId",
                table: "LostItems",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LostItems_TenantId",
                table: "LostItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceHistory_MaintenanceItemId",
                table: "MaintenanceHistory",
                column: "MaintenanceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceHistory_MaintenanceRequestId",
                table: "MaintenanceHistory",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceHistory_TenantId",
                table: "MaintenanceHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceItems_TenantId",
                table: "MaintenanceItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_AssignedTo",
                table: "MaintenanceRequests",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ConversationId",
                table: "MaintenanceRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_MaintenanceItemId",
                table: "MaintenanceRequests",
                column: "MaintenanceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_TenantId",
                table: "MaintenanceRequests",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffTasks_EmergencyIncidents_EmergencyIncidentId",
                table: "StaffTasks",
                column: "EmergencyIncidentId",
                principalTable: "EmergencyIncidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffTasks_MaintenanceRequests_MaintenanceRequestId",
                table: "StaffTasks",
                column: "MaintenanceRequestId",
                principalTable: "MaintenanceRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffTasks_EmergencyIncidents_EmergencyIncidentId",
                table: "StaffTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffTasks_MaintenanceRequests_MaintenanceRequestId",
                table: "StaffTasks");

            migrationBuilder.DropTable(
                name: "BookingChangeHistory");

            migrationBuilder.DropTable(
                name: "EmergencyContacts");

            migrationBuilder.DropTable(
                name: "EmergencyIncidents");

            migrationBuilder.DropTable(
                name: "EmergencyProtocols");

            migrationBuilder.DropTable(
                name: "LostAndFoundCategories");

            migrationBuilder.DropTable(
                name: "LostAndFoundNotifications");

            migrationBuilder.DropTable(
                name: "MaintenanceHistory");

            migrationBuilder.DropTable(
                name: "BookingModifications");

            migrationBuilder.DropTable(
                name: "EmergencyTypes");

            migrationBuilder.DropTable(
                name: "LostAndFoundMatches");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests");

            migrationBuilder.DropTable(
                name: "FoundItems");

            migrationBuilder.DropTable(
                name: "LostItems");

            migrationBuilder.DropTable(
                name: "MaintenanceItems");

            migrationBuilder.DropIndex(
                name: "IX_StaffTasks_EmergencyIncidentId",
                table: "StaffTasks");

            migrationBuilder.DropIndex(
                name: "IX_StaffTasks_MaintenanceRequestId",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "EmergencyIncidentId",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "MaintenanceRequestId",
                table: "StaffTasks");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "RequestItems");
        }
    }
}
