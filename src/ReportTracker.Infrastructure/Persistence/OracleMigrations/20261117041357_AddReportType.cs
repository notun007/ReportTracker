using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ReportTracker.Infrastructure.Persistence.OracleMigrations
{
    /// <inheritdoc />
    public partial class AddReportType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "report_type_id",
                table: "rt_report_definitions",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "rt_report_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    name = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rt_report_types_id", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "rt_report_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "RIT" },
                    { 2, "None RIT" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_rt_report_definitions_report_type_id",
                table: "rt_report_definitions",
                column: "report_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_rt_report_definitions_rt_report_types_report_type_id",
                table: "rt_report_definitions",
                column: "report_type_id",
                principalTable: "rt_report_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_rt_report_definitions_rt_report_types_report_type_id",
                table: "rt_report_definitions");

            migrationBuilder.DropTable(
                name: "rt_report_types");

            migrationBuilder.DropIndex(
                name: "ix_rt_report_definitions_report_type_id",
                table: "rt_report_definitions");

            migrationBuilder.DropColumn(
                name: "report_type_id",
                table: "rt_report_definitions");
        }
    }
}
