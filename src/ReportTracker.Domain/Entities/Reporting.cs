using ReportTracker.Domain.Common;
using ReportTracker.Domain.Enums;

namespace ReportTracker.Domain.Entities;

public sealed class SubmissionRecipient : Entity
{
    public required string Name { get; set; }
    public ICollection<SubmissionRecipientDepartment> Departments { get; set; } = [];
}

public sealed class SubmissionRecipientDepartment : Entity
{
    public Guid SubmissionRecipientId { get; set; }
    public required string Name { get; set; }
}

public sealed class ReportSubmissionDepartment
{
    public Guid ReportDefinitionId { get; set; }
    public Guid DepartmentId { get; set; }
    public SubmissionRecipientDepartment Department { get; set; } = null!;
}

public sealed class ReportDefinition : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid OwnerDivisionId { get; set; }
    public Guid? SubmittingDivisionId { get; set; }
    public Guid? ReceivingDivisionId { get; set; }
    public required string BbRecipient { get; set; }
    public Guid? SubmissionRecipientId { get; set; }
    public SubmissionRecipient? SubmissionRecipient { get; set; }
    public ICollection<ReportSubmissionDepartment> SubmissionDepartments { get; set; } = [];
    public string? InstructionReference { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<DefinitionVersion> Versions { get; set; } = [];

    public string SubmissionDestination() => FormatSubmissionDestination(SubmissionRecipient?.Name ?? BbRecipient,
        SubmissionDepartments.Select(x => x.Department.Name));

    public static string FormatSubmissionDestination(string organization, IEnumerable<string> departmentNames)
    {
        var departments = departmentNames.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return departments.Length == 0 ? organization : $"{organization} — {string.Join("; ", departments)}";
    }
}

public sealed class DefinitionVersion : Entity
{
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;
    public int VersionNumber { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Frequency Frequency { get; set; }
    public TimeOnly DueTime { get; set; }
    public int ComponentDeadlineOffsetDays { get; set; } = 1;
    public string TimeZoneId { get; set; } = "Asia/Dhaka";
    public string? ScheduleRuleJson { get; set; }
    public bool ReceiptConfirmationRequired { get; set; }
}

public sealed class ComponentDefinition : Entity
{
    public Guid DefinitionVersionId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public int Sequence { get; set; }
    public Guid ProviderDivisionId { get; set; }
    public Guid? PrimaryOfficerId { get; set; }
    public Guid? DeputyOfficerId { get; set; }
    public Guid ReceivingDivisionId { get; set; }
    public bool ReceiptConfirmationRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public string? DeadlineRuleJson { get; set; }
    public string? ContributorsJson { get; set; }
}

public sealed class ReportCycle : Entity
{
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;
    public Guid DefinitionVersionId { get; set; }
    public required string PeriodKey { get; set; }
    public required string PeriodLabel { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public ICollection<TrackingObligation> Obligations { get; set; } = [];
}

public sealed class TrackingObligation : Entity
{
    public string? ContributorsJson { get; set; }
    public bool IsRequired { get; set; } = true;
    public Guid ReportCycleId { get; set; }
    public ReportCycle ReportCycle { get; set; } = null!;
    public Guid? ParentObligationId { get; set; }
    public Guid? CorrectionCaseId { get; set; }
    public ObligationType Type { get; set; }
    public required string Name { get; set; }
    public Guid AccountableDivisionId { get; set; }
    public Guid? ReceivingDivisionId { get; set; }
    public Guid? PrimaryOfficerId { get; set; }
    public Guid? DeputyOfficerId { get; set; }
    public required string Recipient { get; set; }
    public DateTime OriginalDueAtUtc { get; set; }
    public DateTime EffectiveDueAtUtc { get; set; }
    public SendingStatus SendingStatus { get; set; } = SendingStatus.NotSent;
    public ReceiptStatus ReceiptStatus { get; set; } = ReceiptStatus.NotRequired;
    public EvidenceStatus EvidenceStatus { get; set; } = EvidenceStatus.NotRequired;
    public CorrectionStatus CorrectionStatus { get; set; } = CorrectionStatus.None;
    public ExceptionStatus ExceptionStatus { get; set; } = ExceptionStatus.None;
    public ICollection<SendingEvent> SendingEvents { get; set; } = [];
}

public sealed class ChannelRequirement : Entity
{
    public Guid TrackingObligationId { get; set; }
    public required string Channel { get; set; }
    public ChannelRequirementType RequirementType { get; set; }
    public bool IsFulfilled { get; set; }
}
