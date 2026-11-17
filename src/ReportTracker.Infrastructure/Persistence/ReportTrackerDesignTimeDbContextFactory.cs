using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Oracle.EntityFrameworkCore;

namespace ReportTracker.Infrastructure.Persistence;

public sealed class ReportTrackerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReportTrackerDbContext>
{
    public ReportTrackerDbContext CreateDbContext(string[] args)
    {
        const string secretsId = "ReportTracker-LocalDevelopment-28b45d34-1c42-4cee-9223-7a10cae95cc7";
        var secretsPath = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", secretsId, "secrets.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets", secretsId, "secrets.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(secretsPath, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException(
                "Connection string 'Oracle' is required for design-time operations. " +
                "Set ConnectionStrings__Oracle or configure it in API user secrets.");
        return new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseOracle(connectionString, oracle =>
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)
                    .MigrationsHistoryTable("rt_ef_migrations_history"))
            .Options);
    }
}
