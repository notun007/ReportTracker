using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations;

public partial class SchedulePresetDisplayOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            schema: "tracker",
            table: "SchedulePresets",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("""
            UPDATE [tracker].[SchedulePresets]
            SET [DisplayOrder] = CASE [RuleCode]
                WHEN N'weekly' THEN 1
                WHEN N'twice-monthly' THEN 2
                WHEN N'monthly' THEN 3
                WHEN N'quarterly' THEN 4
                WHEN N'half-yearly' THEN 5
                WHEN N'yearly' THEN 6
                ELSE 7
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisplayOrder",
            schema: "tracker",
            table: "SchedulePresets");
    }
}
