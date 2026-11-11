using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceivingDivisionResponsibilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingObligations_ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations",
                column: "ReceivingDivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackingObligations_Divisions_ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations",
                column: "ReceivingDivisionId",
                principalSchema: "tracker",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Automatically generated internal tasks already store the receiving division's GUID as Recipient.
            // Preserve that explicit mapping; do not guess ownership from a person's or division's free-text name.
            migrationBuilder.Sql("""
                UPDATE obligation
                SET ReceivingDivisionId = division.Id
                FROM tracker.TrackingObligations AS obligation
                INNER JOIN tracker.Divisions AS division ON division.Id = TRY_CONVERT(uniqueidentifier, obligation.Recipient)
                WHERE obligation.Type = 1 AND obligation.ReceivingDivisionId IS NULL AND LEN(obligation.Recipient) = 36;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackingObligations_Divisions_ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations");

            migrationBuilder.DropIndex(
                name: "IX_TrackingObligations_ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations");

            migrationBuilder.DropColumn(
                name: "ReceivingDivisionId",
                schema: "tracker",
                table: "TrackingObligations");
        }
    }
}
