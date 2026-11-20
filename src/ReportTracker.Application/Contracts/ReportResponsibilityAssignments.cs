using System.ComponentModel.DataAnnotations;

namespace ReportTracker.Application.Contracts;

public sealed record AssigneePair(Guid PrimaryAssigneeId, Guid? AlternateAssigneeId);
public sealed record ResponsibilitySection(Guid? DivisionId, string Division,
    IReadOnlyList<LookupItem> Assignees, Guid? PrimaryAssigneeId, Guid? AlternateAssigneeId);
public sealed record ReportResponsibilityAssignments(Guid ReportId, string Report, string RowVersion,
    ResponsibilitySection Controller, ResponsibilitySection Receiver, ResponsibilitySection FinalSubmission,
    int ExistingReceiverCount);
public sealed record SaveReportResponsibilityAssignmentsRequest(
    [Required] AssigneePair Controller, [Required] AssigneePair Receiver,
    [Required] AssigneePair FinalSubmission, [Required] string RowVersion);
