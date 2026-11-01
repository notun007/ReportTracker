using ReportTracker.Application.Contracts;

namespace ReportTracker.Application.Abstractions;

public interface ITrackingService
{
    Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<ObligationListItemDto?> GetObligationAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> RecordSendingAsync(Guid obligationId, RecordSendingCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<SendingHistoryDto>?> GetHistoryAsync(Guid obligationId, CancellationToken cancellationToken);
}
