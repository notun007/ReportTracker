using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchedulePresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulePresets",
                schema: "tracker",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MonthsPerPeriod = table.Column<int>(type: "int", nullable: false),
                    PeriodEndDays = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeadlineMonthOffset = table.Column<int>(type: "int", nullable: false),
                    FixedDeadlineDay = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulePresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePresets_RuleCode_Version",
                schema: "tracker",
                table: "SchedulePresets",
                columns: new[] { "RuleCode", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulePresets",
                schema: "tracker");
        }
    }
}
