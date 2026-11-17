using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Oracle");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Oracle' is not configured. Set ConnectionStrings__Oracle or use development user secrets.");

        services.AddDbContext<ReportTrackerDbContext>(options =>
            options.UseOracle(connectionString, oracle =>
                oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)
                    .MigrationsHistoryTable("rt_ef_migrations_history")));
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<ICycleGenerationService, CycleGenerationService>();
        services.AddScoped<INotificationAutomationService, NotificationAutomationService>();
        services.AddScoped<TrackingAccess>();
        if (configuration.GetValue("Automation:Enabled", true))
        {
            services.AddHostedService<OperationsAutomationWorker>();
            services.AddHostedService<NotificationAutomationWorker>();
        }
        return services;
    }
}
