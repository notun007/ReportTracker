using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SmartWorkflowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComponentDeadlineOffsetDays",
                schema: "tracker",
                table: "DefinitionVersions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "tracker",
                table: "ComponentDefinitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvidenceRequired",
                schema: "tracker",
                table: "ComponentDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                schema: "tracker",
                table: "ComponentDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                schema: "tracker",
                table: "ComponentDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComponentDeadlineOffsetDays",
                schema: "tracker",
                table: "DefinitionVersions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "EvidenceRequired",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "tracker",
                table: "ComponentDefinitions");
        }
    }
}
