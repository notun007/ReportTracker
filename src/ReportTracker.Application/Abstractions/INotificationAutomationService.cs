namespace ReportTracker.Application.Abstractions;

public interface INotificationAutomationService
{
    Task<int> PlanAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<int> DispatchAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
