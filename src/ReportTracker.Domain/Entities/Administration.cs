using ReportTracker.Domain.Common;
using ReportTracker.Domain.Enums;

namespace ReportTracker.Domain.Entities;

public sealed class AppRole : Entity { public required string Name { get; set; } public string? Description { get; set; } }
public sealed class Permission : Entity { public required string Code { get; set; } public required string Name { get; set; } }
public sealed class UserRole : Entity { public Guid UserId { get; set; } public Guid RoleId { get; set; } public Guid? DivisionId { get; set; } public Guid? ReportDefinitionId { get; set; } }
public sealed class RolePermission : Entity { public Guid RoleId { get; set; } public Guid PermissionId { get; set; } }

public sealed class ResponsibilityAssignment : Entity
{
    public Guid ReportDefinitionId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid PrimaryOfficerId { get; set; }
    public Guid? DeputyOfficerId { get; set; }
    public required string ResponsibilityType { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
}

public sealed class ScheduleRule : Entity
{
    public Guid DefinitionVersionId { get; set; }
    public required string PeriodConvention { get; set; }
    public int? FixedDay { get; set; }
    public int? OffsetDays { get; set; }
    public bool UsesBusinessDays { get; set; }
    public bool HolidayAdjustmentAuthorized { get; set; }
}

public sealed class StatusAmendment : Entity
{
    public Guid TrackingObligationId { get; set; }
    public Guid? ReplacedSendingEventId { get; set; }
    public required string PreviousStatus { get; set; }
    public required string ReplacementStatus { get; set; }
    public required string Reason { get; set; }
    public Guid ActingUserId { get; set; }
}

public sealed class ExtensionDecision : Entity
{
    public Guid ExtensionRequestId { get; set; }
    public Guid DecidedByUserId { get; set; }
    public ExtensionStatus Decision { get; set; }
    public required string Reason { get; set; }
    public DateTime DecidedAtUtc { get; set; }
}

public sealed class ReminderPolicy : Entity
{
    public Guid? ReportDefinitionId { get; set; }
    public required string ObligationCategory { get; set; }
    public string LeadDaysCsv { get; set; } = "2,1,0";
    public TimeOnly DispatchTime { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Dhaka";
    public required string ChannelsCsv { get; set; }
    public int EscalationAfterDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class NotificationJob : Entity
{
    public Guid TrackingObligationId { get; set; }
    public Guid ReminderPolicyId { get; set; }
    public required string LogicalKey { get; set; }
    public DateOnly LocalDate { get; set; }
    public required string Channel { get; set; }
    public DateTime DueAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
}

public sealed class NotificationDelivery : Entity
{
    public Guid NotificationJobId { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class CalendarLink : Entity
{
    public Guid TrackingObligationId { get; set; }
    public Guid UserId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalEventId { get; set; }
    public DateTime LastSynchronizedAtUtc { get; set; }
}

public sealed class HolidayCalendar : Entity
{
    public required string Name { get; set; }
    public int Year { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Dhaka";
    public ICollection<HolidayDate> Dates { get; set; } = [];
}

public sealed class HolidayDate : Entity
{
    public Guid HolidayCalendarId { get; set; }
    public HolidayCalendar HolidayCalendar { get; set; } = null!;
    public DateOnly Date { get; set; }
    public required string Name { get; set; }
}
