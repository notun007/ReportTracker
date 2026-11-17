using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oracle.EntityFrameworkCore;
using ReportTracker.Domain.Common;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Tests;

public sealed class OracleModelConventionTests
{
    [Fact]
    public void Model_uses_rt_prefixed_snake_case_identifiers_and_oracle_types()
    {
        var options = new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseOracle(
                "User Id=model_test;Password=model_test;Data Source=localhost/model_test",
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;

        using var db = new ReportTrackerDbContext(options);
        var entityTypes = db.Model.GetEntityTypes().ToArray();

        Assert.Equal(38, entityTypes.Length);
        Assert.All(entityTypes, entityType =>
        {
            var tableName = Assert.IsType<string>(entityType.GetTableName());
            Assert.StartsWith("rt_", tableName);
            Assert.Equal(tableName.ToLowerInvariant(), tableName);
            Assert.DoesNotContain(entityType.GetProperties(), property =>
                property.GetColumnName().Any(char.IsUpper));
        });

        var report = db.Model.FindEntityType(typeof(ReportDefinition))!;
        Assert.Equal("rt_report_definitions", report.GetTableName());
        Assert.Equal("owner_division_id", report.FindProperty(nameof(ReportDefinition.OwnerDivisionId))!.GetColumnName());
        Assert.Equal("RAW(16)", report.FindProperty(nameof(ReportDefinition.Id))!.GetColumnType());
        Assert.Equal("NUMBER(1)", report.FindProperty(nameof(ReportDefinition.IsActive))!.GetColumnType());
        Assert.Equal("report_type_id", report.FindProperty(nameof(ReportDefinition.ReportTypeId))!.GetColumnName());
        Assert.Equal("rt_report_types", db.Model.FindEntityType(typeof(ReportType))!.GetTableName());

        var rowVersion = report.FindProperty(nameof(Entity.RowVersion))!;
        Assert.Equal("RAW(8)", rowVersion.GetColumnType());
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.Never, rowVersion.ValueGenerated);

        var version = db.Model.FindEntityType(typeof(DefinitionVersion))!;
        Assert.Equal("DATE", version.FindProperty(nameof(DefinitionVersion.EffectiveFrom))!.GetColumnType());
        Assert.Equal("INTERVAL DAY(0) TO SECOND(6)", version.FindProperty(nameof(DefinitionVersion.DueTime))!.GetColumnType());
    }
}
