using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations;

public partial class ReportingFrequencies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReportingFrequencies",
            schema: "tracker",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReportingFrequencies", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ReportingFrequencies_Code",
            schema: "tracker",
            table: "ReportingFrequencies",
            column: "Code",
            unique: true);

        migrationBuilder.InsertData(
            schema: "tracker",
            table: "ReportingFrequencies",
            columns: new[] { "Id", "Code", "Name", "DisplayOrder" },
            values: new object[,]
            {
                { 1, "weekly", "Weakly", 1 },
                { 2, "twice-monthly", "Biweekly", 2 },
                { 3, "monthly", "Monthly", 3 },
                { 4, "quarterly", "Quarterly", 4 },
                { 5, "half-yearly", "Half-Yearly", 5 },
                { 6, "yearly", "Yearly", 6 }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReportingFrequencies", schema: "tracker");
    }
}
