using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations;

public partial class MonthlyPresetDays : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "PeriodEndDays", schema: "tracker", table: "SchedulePresets",
            type: "nvarchar(47)", maxLength: 47, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(16)", oldMaxLength: 16);
        migrationBuilder.AlterColumn<string>(name: "WeeklyStartDays", schema: "tracker", table: "SchedulePresets",
            type: "nvarchar(35)", maxLength: 35, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(11)", oldMaxLength: 11, oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [tracker].[SchedulePresets] WHERE LEN([WeeklyStartDays]) > 11 OR LEN([PeriodEndDays]) > 16)
                THROW 50001, 'Saved schedule presets exceed the previous column lengths.', 1;
            """);
        migrationBuilder.AlterColumn<string>(name: "PeriodEndDays", schema: "tracker", table: "SchedulePresets",
            type: "nvarchar(16)", maxLength: 16, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(47)", oldMaxLength: 47);
        migrationBuilder.AlterColumn<string>(name: "WeeklyStartDays", schema: "tracker", table: "SchedulePresets",
            type: "nvarchar(11)", maxLength: 11, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(35)", oldMaxLength: 35, oldNullable: true);
    }
}
