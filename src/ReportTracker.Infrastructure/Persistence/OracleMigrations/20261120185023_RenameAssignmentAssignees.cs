using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.OracleMigrations
{
    /// <inheritdoc />
    public partial class RenameAssignmentAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "primary_officer_id",
                table: "rt_tracking_obligations",
                newName: "primary_assignee_id");

            migrationBuilder.RenameColumn(
                name: "deputy_officer_id",
                table: "rt_tracking_obligations",
                newName: "alternate_assignee_id");

            migrationBuilder.Sql("""
                UPDATE "rt_reminder_policies"
                SET "recipients_csv" = REGEXP_REPLACE("recipients_csv",
                    '(^|,)([[:space:]]*)Deputy([[:space:]]*)(,|$)', '\1\2Alternate\3\4', 1, 0, 'i')
                WHERE REGEXP_LIKE("recipients_csv", '(^|,)[[:space:]]*Deputy[[:space:]]*(,|$)', 'i')
                """);

            migrationBuilder.RenameColumn(
                name: "primary_officer_id",
                table: "rt_responsibility_assignments",
                newName: "primary_assignee_id");

            migrationBuilder.RenameColumn(
                name: "deputy_officer_id",
                table: "rt_responsibility_assignments",
                newName: "alternate_assignee_id");

            migrationBuilder.RenameColumn(
                name: "primary_officer_id",
                table: "rt_component_definitions",
                newName: "primary_assignee_id");

            migrationBuilder.RenameColumn(
                name: "deputy_officer_id",
                table: "rt_component_definitions",
                newName: "alternate_assignee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "rt_reminder_policies"
                SET "recipients_csv" = REGEXP_REPLACE("recipients_csv",
                    '(^|,)([[:space:]]*)Alternate([[:space:]]*)(,|$)', '\1\2Deputy\3\4', 1, 0, 'i')
                WHERE REGEXP_LIKE("recipients_csv", '(^|,)[[:space:]]*Alternate[[:space:]]*(,|$)', 'i')
                """);
            migrationBuilder.RenameColumn(
                name: "primary_assignee_id",
                table: "rt_tracking_obligations",
                newName: "primary_officer_id");

            migrationBuilder.RenameColumn(
                name: "alternate_assignee_id",
                table: "rt_tracking_obligations",
                newName: "deputy_officer_id");

            migrationBuilder.RenameColumn(
                name: "primary_assignee_id",
                table: "rt_responsibility_assignments",
                newName: "primary_officer_id");

            migrationBuilder.RenameColumn(
                name: "alternate_assignee_id",
                table: "rt_responsibility_assignments",
                newName: "deputy_officer_id");

            migrationBuilder.RenameColumn(
                name: "primary_assignee_id",
                table: "rt_component_definitions",
                newName: "primary_officer_id");

            migrationBuilder.RenameColumn(
                name: "alternate_assignee_id",
                table: "rt_component_definitions",
                newName: "deputy_officer_id");
        }
    }
}
