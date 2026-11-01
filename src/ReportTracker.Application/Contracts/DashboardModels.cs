namespace ReportTracker.Application.Contracts;

public sealed record DashboardSummaryDto(
    int MyPending,
    int DueToday,
    int Overdue,
    int EvidencePending,
    IReadOnlyList<ObligationListItemDto> Upcoming,
    IReadOnlyList<StatusCountDto> Performance);

public sealed record ObligationListItemDto(
    Guid Id,
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
    bool IsAssignedToMe);

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
