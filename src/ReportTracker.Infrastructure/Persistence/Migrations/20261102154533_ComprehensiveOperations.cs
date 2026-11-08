using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComprehensiveOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                schema: "tracker",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DailyOverdueReminders",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursEnd",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietHoursStart",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientsCsv",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkingDaysCsv",
                schema: "tracker",
                table: "ReminderPolicies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                schema: "tracker",
                table: "NotificationJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TrackingObligationId",
                schema: "tracker",
                table: "EvidenceDocuments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MobileNumber",
                schema: "tracker",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyOverdueReminders",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnd",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "QuietHoursStart",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "RecipientsCsv",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "WorkingDaysCsv",
                schema: "tracker",
                table: "ReminderPolicies");

            migrationBuilder.DropColumn(
                name: "Recipient",
                schema: "tracker",
                table: "NotificationJobs");

            migrationBuilder.DropColumn(
                name: "TrackingObligationId",
                schema: "tracker",
                table: "EvidenceDocuments");
        }
    }
}
