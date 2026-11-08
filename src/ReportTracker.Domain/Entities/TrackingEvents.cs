using ReportTracker.Domain.Common;
using ReportTracker.Domain.Enums;

namespace ReportTracker.Domain.Entities;

public sealed class SendingEvent : Entity
{
    public Guid TrackingObligationId { get; set; }
    public TrackingObligation TrackingObligation { get; set; } = null!;
    public Guid ActingUserId { get; set; }
    public DateTime ActualSentAtUtc { get; set; }
    public required string Recipient { get; set; }
    public required string Channel { get; set; }
    public string? Reference { get; set; }
    public bool IsPartial { get; set; }
    public string? ScopeNames { get; set; }
    public string? TrackingRemarks { get; set; }
    public bool IsReversed { get; set; }
}

public sealed class ReceiptConfirmation : Entity
{
    public Guid SendingEventId { get; set; }
    public Guid ActingUserId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsDisputed { get; set; }
    public string? Reason { get; set; }
}

public sealed class EvidenceDocument : Entity
{
    public Guid? TrackingObligationId { get; set; }
    public Guid? SendingEventId { get; set; }
    public Guid? ExtensionRequestId { get; set; }
    public EvidencePurpose Purpose { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public EvidenceStatus Status { get; set; } = EvidenceStatus.Provided;
    public Guid UploadedByUserId { get; set; }
}

public sealed class EvidenceDecision : Entity
{
    public Guid EvidenceDocumentId { get; set; }
    public Guid DecidedByUserId { get; set; }
    public EvidenceStatus Decision { get; set; }
    public string? Reason { get; set; }
    public DateTime DecidedAtUtc { get; set; }
}

public sealed class CorrectionCase : Entity
{
    public Guid ReportCycleId { get; set; }
    public Guid? OriginalSendingEventId { get; set; }
    public required string ReasonCategory { get; set; }
    public string? Explanation { get; set; }
    public required string AffectedComponentNames { get; set; }
    public bool IsFullScope { get; set; }
    public CorrectionStatus Status { get; set; } = CorrectionStatus.Open;
    public DateTime DueAtUtc { get; set; }
}

public sealed class ExtensionRequest : Entity
{
    public Guid TrackingObligationId { get; set; }
    public DateTime OriginalDueAtUtc { get; set; }
    public DateTime RequestedDueAtUtc { get; set; }
    public required string Reason { get; set; }
    public string? PermissionReference { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid ApproverUserId { get; set; }
    public ExtensionStatus Status { get; set; } = ExtensionStatus.Pending;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class AuditEvent : Entity
{
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public required string Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string DataJson { get; set; }
    public string? CorrelationId { get; set; }
}
