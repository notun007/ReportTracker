using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportTracker.Infrastructure.Persistence.OracleMigrations
{
    /// <inheritdoc />
    public partial class ScopedAccessManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "rt_user_roles",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "rt_app_roles",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"rt_app_roles\" SET \"code\" = 'ROLE_' || RAWTOHEX(\"id\") WHERE \"code\" IS NULL");
            migrationBuilder.AlterColumn<string>(name: "code", table: "rt_app_roles", type: "NVARCHAR2(64)",
                maxLength: 64, nullable: false, oldClrType: typeof(string), oldType: "NVARCHAR2(64)", oldMaxLength: 64, oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "rt_app_roles",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "rt_app_roles",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "uq_rt_app_roles_code",
                table: "rt_app_roles",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_rt_app_roles_code",
                table: "rt_app_roles");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "rt_user_roles");

            migrationBuilder.DropColumn(
                name: "code",
                table: "rt_app_roles");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "rt_app_roles");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "rt_app_roles");
        }
    }
}
