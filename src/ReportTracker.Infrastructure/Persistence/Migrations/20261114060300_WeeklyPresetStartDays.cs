using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations;

public partial class WeeklyPresetStartDays : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "WeeklyStartDays", schema: "tracker", table: "SchedulePresets",
            type: "nvarchar(11)", maxLength: 11, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WeeklyStartDays", schema: "tracker", table: "SchedulePresets");
    }
}
