using ReportTracker.Domain.Common;
using ReportTracker.Domain.Enums;

namespace ReportTracker.Domain.Entities;

public sealed class ReportDefinition : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid OwnerDivisionId { get; set; }
    public required string BbRecipient { get; set; }
    public string? InstructionReference { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<DefinitionVersion> Versions { get; set; } = [];
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
    public string TimeZoneId { get; set; } = "Asia/Dhaka";
    public string? ScheduleRuleJson { get; set; }
    public bool ReceiptConfirmationRequired { get; set; }
}

public sealed class ComponentDefinition : Entity
{
    public Guid DefinitionVersionId { get; set; }
    public required string Name { get; set; }
    public Guid ProviderDivisionId { get; set; }
    public Guid? PrimaryOfficerId { get; set; }
    public Guid? DeputyOfficerId { get; set; }
    public Guid ReceivingDivisionId { get; set; }
    public bool ReceiptConfirmationRequired { get; set; }
    public string? DeadlineRuleJson { get; set; }
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
    public Guid ReportCycleId { get; set; }
    public ReportCycle ReportCycle { get; set; } = null!;
    public Guid? ParentObligationId { get; set; }
    public Guid? CorrectionCaseId { get; set; }
    public ObligationType Type { get; set; }
    public required string Name { get; set; }
    public Guid AccountableDivisionId { get; set; }
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
