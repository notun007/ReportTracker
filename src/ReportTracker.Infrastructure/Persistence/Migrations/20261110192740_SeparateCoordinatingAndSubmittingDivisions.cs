using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateCoordinatingAndSubmittingDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                EXEC(N'UPDATE report
                SET SubmittingDivisionId = COALESCE((
                    SELECT TOP (1) assignment.DivisionId
                    FROM tracker.ResponsibilityAssignments AS assignment
                    INNER JOIN tracker.Divisions AS division ON division.Id = assignment.DivisionId
                    INNER JOIN tracker.Users AS officer ON officer.Id = assignment.PrimaryOfficerId
                        AND officer.DivisionId = assignment.DivisionId AND officer.IsActive = 1
                    WHERE assignment.ReportDefinitionId = report.Id AND assignment.ResponsibilityType = N''MainSender''
                        AND assignment.EffectiveFromUtc <= GETUTCDATE()
                        AND (assignment.EffectiveToUtc IS NULL OR assignment.EffectiveToUtc > GETUTCDATE())
                    ORDER BY assignment.EffectiveFromUtc DESC, assignment.Id
                ), report.OwnerDivisionId)
                FROM tracker.ReportDefinitions AS report;');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "SubmittingDivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDefinitions_Divisions_SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "SubmittingDivisionId",
                principalSchema: "tracker",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportDefinitions_Divisions_SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "SubmittingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");
        }
    }
}
