using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportTracker.Application.Abstractions;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReportTracker");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'ReportTracker' is not configured. Set ConnectionStrings__ReportTracker or use development user secrets.");

        services.AddDbContext<ReportTrackerDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5)));
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<TrackingAccess>();
        return services;
    }
}
