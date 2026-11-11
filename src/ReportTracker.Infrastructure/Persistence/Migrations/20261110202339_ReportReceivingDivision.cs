using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportReceivingDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "ReceivingDivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDefinitions_Divisions_ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions",
                column: "ReceivingDivisionId",
                principalSchema: "tracker",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportDefinitions_Divisions_ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ReportDefinitions_ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");

            migrationBuilder.DropColumn(
                name: "ReceivingDivisionId",
                schema: "tracker",
                table: "ReportDefinitions");
        }
    }
}
