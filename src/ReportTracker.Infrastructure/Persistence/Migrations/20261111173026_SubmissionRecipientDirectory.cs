using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionRecipientDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubmissionRecipients",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRecipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionRecipientDepartments",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionRecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionRecipientDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionRecipientDepartments_SubmissionRecipients_SubmissionRecipientId",
                        column: x => x.SubmissionRecipientId,
                        principalSchema: "tracker",
                        principalTable: "SubmissionRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportSubmissionDepartments",
                schema: "tracker",
                columns: table => new
                {
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSubmissionDepartments", x => new { x.ReportDefinitionId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_ReportSubmissionDepartments_ReportDefinitions_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalSchema: "tracker",
                        principalTable: "ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportSubmissionDepartments_SubmissionRecipientDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "tracker",
                        principalTable: "SubmissionRecipientDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "SubmissionRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissionDepartments_DepartmentId",
                schema: "tracker",
                table: "ReportSubmissionDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionRecipientDepartments_SubmissionRecipientId_Name",
                schema: "tracker",
                table: "SubmissionRecipientDepartments",
                columns: new[] { "SubmissionRecipientId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionRecipients_Name",
                schema: "tracker",
                table: "SubmissionRecipients",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDefinitions_SubmissionRecipients_SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "SubmissionRecipientId",
                principalSchema: "tracker",
                principalTable: "SubmissionRecipients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Import existing destination names without changing the report text or historical submissions.
            migrationBuilder.Sql("""
                INSERT INTO [tracker].[SubmissionRecipients] ([Id], [Name], [CreatedAtUtc])
                SELECT NEWID(), [Name], SYSUTCDATETIME()
                FROM (
                    SELECT DISTINCT CONVERT(nvarchar(250), LTRIM(RTRIM([BbRecipient]))) AS [Name]
                    FROM [tracker].[ReportDefinitions]
                    WHERE DATALENGTH(LTRIM(RTRIM([BbRecipient]))) BETWEEN 2 AND 500
                ) AS [LegacyRecipients];

                EXEC(N'
                    UPDATE report
                    SET [SubmissionRecipientId] = recipient.[Id]
                    FROM [tracker].[ReportDefinitions] AS report
                    JOIN [tracker].[SubmissionRecipients] AS recipient
                      ON LTRIM(RTRIM(report.[BbRecipient])) = recipient.[Name]
                    WHERE report.[SubmissionRecipientId] IS NULL;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportDefinitions_SubmissionRecipients_SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropTable(
                name: "ReportSubmissionDepartments",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "SubmissionRecipientDepartments",
                schema: "tracker");

            migrationBuilder.DropTable(
                name: "SubmissionRecipients",
                schema: "tracker");

            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "SubmissionRecipientId",
                schema: "tracker",
                table: "ReportDefinitions");
        }
    }
}
