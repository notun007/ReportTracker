using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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
        var connectionString = configuration.GetConnectionString("ReportTracker")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ReportTrackerDesign;Trusted_Connection=True;TrustServerCertificate=True";
        return new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseSqlServer(connectionString)
            .Options);
    }
}
