using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyComponentDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE component
                SET [ContributorsJson] = (
                    SELECT assignment.[UserId], assignment.[Role]
                    FROM (VALUES (component.[PrimaryOfficerId], N'LeadPreparer'),
                                 (NULLIF(component.[DeputyOfficerId], component.[PrimaryOfficerId]), N'BackupPreparer')) AS assignment([UserId], [Role])
                    WHERE assignment.[UserId] IS NOT NULL
                    FOR JSON PATH
                )
                FROM [tracker].[ComponentDefinitions] AS component
                WHERE component.[ContributorsJson] IS NULL AND component.[PrimaryOfficerId] IS NOT NULL;

                UPDATE component
                SET [PrimaryOfficerId] = lead.[UserId],
                    [DeputyOfficerId] = NULL,
                    [ContributorsJson] = (
                        SELECT contributor.[UserId], contributor.[Role]
                        FROM OPENJSON(component.[ContributorsJson])
                        WITH ([UserId] uniqueidentifier '$.UserId', [Role] nvarchar(32) '$.Role') AS contributor
                        FOR JSON PATH
                    )
                FROM [tracker].[ComponentDefinitions] AS component
                CROSS APPLY (
                    SELECT TOP (1) contributor.[UserId]
                    FROM OPENJSON(CASE WHEN ISJSON(component.[ContributorsJson]) = 1
                                      THEN component.[ContributorsJson] ELSE N'[]' END)
                    WITH ([UserId] uniqueidentifier '$.UserId', [Role] nvarchar(32) '$.Role') AS contributor
                    WHERE contributor.[Role] = N'LeadPreparer' AND contributor.[UserId] IS NOT NULL
                    ORDER BY contributor.[UserId]
                ) AS lead;
                """);

            migrationBuilder.DropColumn(
                name: "EvidenceRequired",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "ReceiptConfirmationRequired",
                schema: "tracker",
                table: "ComponentDefinitions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "tracker",
                table: "ComponentDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptConfirmationRequired",
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
    }
}
