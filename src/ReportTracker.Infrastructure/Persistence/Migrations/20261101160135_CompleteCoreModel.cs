using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCoreModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarLinks",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSynchronizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtensionDecisions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtensionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtensionDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayCalendars",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationJobs",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogicalKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReminderPolicies",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObligationCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadDaysCsv = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispatchTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelsCsv = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EscalationAfterDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResponsibilityAssignments",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeputyOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsibilityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsibilityAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRules",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodConvention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FixedDay = table.Column<int>(type: "int", nullable: true),
                    OffsetDays = table.Column<int>(type: "int", nullable: true),
                    UsesBusinessDays = table.Column<bool>(type: "bit", nullable: false),
                    HolidayAdjustmentAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusAmendments",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplacedSendingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReplacementStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusAmendments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayDates",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolidayCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HolidayDates_HolidayCalendars_HolidayCalendarId",
                        column: x => x.HolidayCalendarId,
                        principalSchema: "tracker",
                        principalTable: "HolidayCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarLinks_TrackingObligationId_UserId_Provider",
                schema: "tracker",
                table: "CalendarLinks",
                columns: new[] { "TrackingObligationId", "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_Name_Year",
                schema: "tracker",
                table: "HolidayCalendars",
                columns: new[] { "Name", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayDates_HolidayCalendarId_Date",
                schema: "tracker",
                table: "HolidayDates",
                columns: new[] { "HolidayCalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationJobs_LogicalKey",
                schema: "tracker",
                table: "NotificationJobs",
                column: "LogicalKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                schema: "tracker",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                schema: "tracker",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "tracker",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId_DivisionId_ReportDefinitionId",
                schema: "tracker",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId", "DivisionId", "ReportDefinitionId" },
                unique: true,
                filter: "[DivisionId] IS NOT NULL AND [ReportDefinitionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarLinks",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ExtensionDecisions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "HolidayDates",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "NotificationDeliveries",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "NotificationJobs",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ReminderPolicies",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ResponsibilityAssignments",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ScheduleRules",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "StatusAmendments",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "HolidayCalendars",
                schema: "tracker");
        }
    }
}
