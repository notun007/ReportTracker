using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tracker");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelRequirements",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequirementType = table.Column<int>(type: "int", nullable: false),
                    IsFulfilled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComponentDefinitions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderDivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeputyOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivingDivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptConfirmationRequired = table.Column<bool>(type: "bit", nullable: false),
                    DeadlineRuleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrectionCases",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalSendingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReasonCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AffectedComponentNames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFullScope = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectionCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Divisions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceDecisions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceDocuments",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SendingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtensionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtensionRequests",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PermissionReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtensionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptConfirmations",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SendingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDisputed = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerDivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BbRecipient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstructionReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportDefinitions_Divisions_OwnerDivisionId",
                        column: x => x.OwnerDivisionId,
                        principalSchema: "tracker",
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalSchema: "tracker",
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalSchema: "tracker",
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DefinitionVersions",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    DueTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScheduleRuleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptConfirmationRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefinitionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefinitionVersions_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalSchema: "tracker",
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportCycles",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PeriodLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportCycles_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalSchema: "tracker",
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackingObligations",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AccountableDivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrimaryOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeputyOfficerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendingStatus = table.Column<int>(type: "int", nullable: false),
                    ReceiptStatus = table.Column<int>(type: "int", nullable: false),
                    EvidenceStatus = table.Column<int>(type: "int", nullable: false),
                    CorrectionStatus = table.Column<int>(type: "int", nullable: false),
                    ExceptionStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingObligations_Divisions_AccountableDivisionId",
                        column: x => x.AccountableDivisionId,
                        principalSchema: "tracker",
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackingObligations_ReportCycles_ReportCycleId",
                        column: x => x.ReportCycleId,
                        principalSchema: "tracker",
                        principalTable: "ReportCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SendingEvents",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingObligationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPartial = table.Column<bool>(type: "bit", nullable: false),
                    ScopeNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackingRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsReversed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SendingEvents_TrackingObligations_TrackingObligationId",
                        column: x => x.TrackingObligationId,
                        principalSchema: "tracker",
                        principalTable: "TrackingObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelRequirements_TrackingObligationId_Channel",
                schema: "tracker",
                table: "ChannelRequirements",
                columns: new[] { "TrackingObligationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefinitionVersions_ReportDefinitionId_VersionNumber",
                schema: "tracker",
                table: "DefinitionVersions",
                columns: new[] { "ReportDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Code",
                schema: "tracker",
                table: "Divisions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportCycles_ReportDefinitionId_PeriodKey",
                schema: "tracker",
                table: "ReportCycles",
                columns: new[] { "ReportDefinitionId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_Code",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_OwnerDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "OwnerDivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_SendingEvents_TrackingObligationId",
                schema: "tracker",
                table: "SendingEvents",
                column: "TrackingObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DivisionId",
                schema: "tracker",
                table: "Teams",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingObligations_AccountableDivisionId",
                schema: "tracker",
                table: "TrackingObligations",
                column: "AccountableDivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingObligations_ReportCycleId",
                schema: "tracker",
                table: "TrackingObligations",
                column: "ReportCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DivisionId",
                schema: "tracker",
                table: "Users",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "tracker",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                schema: "tracker",
                table: "Users",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ChannelRequirements",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ComponentDefinitions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "CorrectionCases",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "DefinitionVersions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "EvidenceDecisions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "EvidenceDocuments",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ExtensionRequests",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ReceiptConfirmations",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "SendingEvents",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "TrackingObligations",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ReportCycles",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "ReportDefinitions",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "Divisions",
                schema: "tracker");
        }
    }
}
