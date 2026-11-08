using System.ComponentModel.DataAnnotations;

namespace ReportTracker.Application.Contracts;

public sealed record EvidenceItem(Guid Id, Guid TrackingObligationId, string OriginalFileName, string ContentType,
    long SizeBytes, string Sha256, string Status, string Purpose, DateTime UploadedAtUtc, string UploadedBy);
public sealed record EvidenceDecisionRequest([Required] string Decision, [StringLength(1000)] string? Reason);

public sealed record ReceiptRequest(DateTime ReceivedAtUtc, bool IsDisputed, [StringLength(1000)] string? Reason);
public sealed record ReceiptItem(Guid Id, Guid SendingEventId, DateTime ReceivedAtUtc, bool IsDisputed, string? Reason,
    DateTime RecordedAtUtc, string Actor);

public sealed record CreateExtensionRequest(DateTime RequestedDueAtUtc, [Required, StringLength(1000)] string Reason,
    [StringLength(250)] string? PermissionReference);
public sealed record DecideExtensionRequest([Required] string Decision, [Required, StringLength(1000)] string Reason);
public sealed record ExtensionItem(Guid Id, Guid TrackingObligationId, string Report, string Obligation, string Period,
    DateTime OriginalDueAtUtc, DateTime RequestedDueAtUtc, string Reason, string? PermissionReference,
    string RequestedBy, string Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionReason);

public sealed record AmendmentRequest([Required] string ReplacementStatus, [Required, StringLength(1000)] string Reason);

public sealed record SaveReminderPolicyRequest([Required, StringLength(150)] string Name, Guid? ReportDefinitionId,
    [Required, StringLength(60)] string ObligationCategory, [Required, StringLength(100)] string LeadDaysCsv,
    TimeOnly DispatchTime, [Required, StringLength(100)] string ChannelsCsv, [StringLength(100)] string? RecipientsCsv,
    bool DailyOverdueReminders, [StringLength(100)] string? WorkingDaysCsv, TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd, [Range(0, 365)] int EscalationAfterDays, bool IsActive);
public sealed record ReminderPolicyItem(Guid Id, string Name, Guid? ReportDefinitionId, string Report,
    string ObligationCategory, string LeadDaysCsv, TimeOnly DispatchTime, string ChannelsCsv, string RecipientsCsv,
    bool DailyOverdueReminders, string WorkingDaysCsv, TimeOnly? QuietHoursStart, TimeOnly? QuietHoursEnd,
    int EscalationAfterDays, bool IsActive);
public sealed record NotificationItem(Guid JobId, Guid ObligationId, string Report, string Obligation, string Recipient,
    string Channel, DateTime DueAtUtc, string Status, int AttemptCount, DateTime? AttemptedAtUtc,
    bool? Succeeded, string? ProviderReference, string? FailureReason);

public sealed record CalendarItem(Guid ObligationId, Guid CycleId, string Report, string Obligation, string Period,
    string Type, string Owner, DateTime DueAtUtc, string Timeliness, string SendingStatus, bool HasApprovedExtension);
public sealed record DivisionPerformanceItem(string Division, int Total, int SentOnTime, int SentLate, int Overdue);
public sealed record ManagementAnalyticsDto(int Total, int Submitted, int Overdue, int ReportsAtRisk,
    int PendingExtensions, int FailedNotifications, decimal OnTimeRate, IReadOnlyList<DivisionPerformanceItem> Divisions);
public sealed record GenerationResult(int ReportsEvaluated, int CyclesCreated, int ComponentsCreated, IReadOnlyList<string> Warnings);

public sealed record ChangeUserStatusRequest(bool IsActive);
public sealed record ResetPasswordRequest([Required, StringLength(128, MinimumLength = 12)] string NewPassword);
