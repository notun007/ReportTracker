using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.OracleMigrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateReportCycleDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE
                    duplicate_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO duplicate_count
                    FROM (
                        SELECT "report_definition_id", "period_start"
                        FROM "rt_report_cycles"
                        GROUP BY "report_definition_id", "period_start"
                        HAVING COUNT(*) > 1
                    );
                    IF duplicate_count > 0 THEN
                        RAISE_APPLICATION_ERROR(-20001, 'Duplicate report-cycle period_start values exist. Resolve them before applying this migration.');
                    END IF;

                    SELECT COUNT(*) INTO duplicate_count
                    FROM (
                        SELECT "report_definition_id", "period_end"
                        FROM "rt_report_cycles"
                        GROUP BY "report_definition_id", "period_end"
                        HAVING COUNT(*) > 1
                    );
                    IF duplicate_count > 0 THEN
                        RAISE_APPLICATION_ERROR(-20002, 'Duplicate report-cycle period_end values exist. Resolve them before applying this migration.');
                    END IF;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_rt_report_cycles_report_definition_id_period_end",
                table: "rt_report_cycles",
                columns: new[] { "report_definition_id", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_rt_report_cycles_report_definition_id_period_start",
                table: "rt_report_cycles",
                columns: new[] { "report_definition_id", "period_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_rt_report_cycles_report_definition_id_period_end",
                table: "rt_report_cycles");

            migrationBuilder.DropIndex(
                name: "uq_rt_report_cycles_report_definition_id_period_start",
                table: "rt_report_cycles");
        }
    }
}
