using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComponentContributorAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContributorsJson",
                schema: "tracker",
                table: "TrackingObligations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContributorsJson",
                schema: "tracker",
                table: "ComponentDefinitions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributorsJson",
                schema: "tracker",
                table: "TrackingObligations");

            migrationBuilder.DropColumn(
                name: "ContributorsJson",
                schema: "tracker",
                table: "ComponentDefinitions");
        }
    }
}
