namespace ReportTracker.Application.Contracts;

public sealed record DashboardSummaryDto(
    int MyPending,
    int DueToday,
    int Overdue,
    int ReadyForSubmission,
    int EvidencePending,
    IReadOnlyList<ObligationListItemDto> Upcoming,
    IReadOnlyList<StatusCountDto> Performance);

public sealed record ObligationListItemDto(
    Guid Id,
    Guid CycleId,
    string Report,
    string Period,
    string Obligation,
    string Type,
    string Owner,
    DateTime DueAtUtc,
    string Timeliness,
    string SendingStatus,
    string ReceiptStatus,
    string EvidenceStatus,
    string Recipient,
    string RowVersion,
    bool CanRecordSending,
    bool IsAssignedToMe,
    int RequiredComponentCount,
    int CompletedComponentCount,
    bool IsReadyForSubmission,
    Guid DivisionId,
    string Division);

public sealed record StatusCountDto(string Status, int Count);

public sealed record RecordSendingCommand(
    DateTime ActualSentAtUtc,
    string Recipient,
    string Channel,
    string? Reference,
    bool IsPartial,
    IReadOnlyList<string>? ScopeNames,
    string? TrackingRemarks,
    string RowVersion);

public sealed record SendingHistoryDto(Guid Id, DateTime ActualSentAtUtc, DateTime RecordedAtUtc,
    string Actor, string Recipient, string Channel, string? Reference, bool IsPartial,
    string? ScopeNames, string? TrackingRemarks);

public sealed record ReadinessComponentDto(
    Guid Id,
    string Name,
    string Division,
    string Owner,
    DateTime DueAtUtc,
    string Timeliness,
    string SendingStatus,
    string EvidenceStatus,
    string ReceiptStatus,
    bool IsBlocking);

public sealed record SubmissionReadinessDto(
    Guid CycleId,
    Guid MainObligationId,
    string Report,
    string Period,
    DateTime MainDueAtUtc,
    int RequiredComponents,
    int CompletedComponents,
    int BlockingComponents,
    bool IsReady,
    IReadOnlyList<ReadinessComponentDto> Components);
