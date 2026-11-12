using System.ComponentModel.DataAnnotations;

namespace ReportTracker.Application.Contracts;

public sealed record LoginRequest([Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(128)] string Password);
public sealed record SetupRequest(
    [Required, StringLength(40)] string DivisionCode, [Required, StringLength(150)] string DivisionName,
    [Required, StringLength(40)] string EmployeeId, [Required, StringLength(150)] string DisplayName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password, string? SetupKey);
public sealed record SessionUser(Guid Id, string DisplayName, string Email, Guid DivisionId, IReadOnlyList<string> Permissions);
public sealed record SessionResponse(bool SetupRequired, SessionUser? User);
public sealed record LookupItem(Guid Id, string Name);
public sealed record DivisionItem(Guid Id, string Code, string Name, bool IsActive);
public sealed record UserItem(Guid Id, string EmployeeId, string DisplayName, string Email, string? MobileNumber, Guid DivisionId, bool IsActive, IReadOnlyList<string> Roles);
public sealed record CreateUserRequest(
    [Required, StringLength(40)] string EmployeeId, [Required, StringLength(150)] string DisplayName,
    [Required, EmailAddress, StringLength(254)] string Email, [Phone, StringLength(30)] string? MobileNumber, Guid DivisionId,
    [Required, StringLength(128, MinimumLength = 12)] string Password, bool IsCoordinator);
public sealed record CreateDivisionRequest([Required, StringLength(40)] string Code, [Required, StringLength(150)] string Name);
public sealed record CreateReportRequest(
    [Required, StringLength(40)] string Code, [Required, StringLength(150)] string Name,
    [StringLength(1000)] string? Description, Guid OwnerDivisionId,
    [StringLength(250)] string? BbRecipient, [StringLength(250)] string? InstructionReference,
    string Frequency, TimeOnly? DueTime = null, int ComponentDeadlineOffsetDays = 1,
    Guid? SubmittingDivisionId = null, Guid? PrimarySubmittingOfficerId = null, Guid? DeputySubmittingOfficerId = null,
    Guid? ReceivingDivisionId = null, [MaxLength(100)] IReadOnlyList<ReportComponentRequest>? Components = null,
    Guid? SubmissionRecipientId = null, [MaxLength(20)] IReadOnlyList<Guid>? SubmissionDepartmentIds = null);
public sealed record ReportComponentRequest(
    [Required, StringLength(250)] string Name, [StringLength(1000)] string? Description,
    bool IsRequired, [Range(0, 10000)] int Sequence, Guid ProviderDivisionId, Guid PrimaryOfficerId, Guid? DeputyOfficerId,
    bool ReceiptRequired, bool EvidenceRequired, [Range(0, 365)] int DeadlineOffsetDays = 1,
    [MaxLength(200)] IReadOnlyList<ComponentContributorItem>? Contributors = null);
public sealed record ComponentContributorItem(Guid UserId, string Role, bool RecordsHandover = false);
public sealed record SetComponentContributorsRequest([Required, MaxLength(200)] IReadOnlyList<ComponentContributorItem> Contributors);
public sealed record ReportItem(Guid Id, string Code, string Name, Guid OwnerDivisionId, string BbRecipient, string Frequency, int CycleCount,
    Guid? SubmittingDivisionId = null, Guid? PrimarySubmittingOfficerId = null, Guid? DeputySubmittingOfficerId = null, Guid? ReceivingDivisionId = null,
    Guid? SubmissionRecipientId = null, IReadOnlyList<Guid>? SubmissionDepartmentIds = null, string? SubmissionDestination = null,
    TimeOnly? DueTime = null, int? ComponentDeadlineOffsetDays = null, string? TimeZoneId = null,
    string? InstructionReference = null, string? Description = null);
public sealed record SubmissionRecipientItem(Guid Id, string Name, IReadOnlyList<LookupItem> Departments);
public sealed record CreateSubmissionRecipientRequest([Required, StringLength(250)] string Name);
public sealed record CreateSubmissionDepartmentRequest([Required, StringLength(150)] string Name);
public sealed record CreateSubmissionDepartmentsRequest([Required, MinLength(1), MaxLength(20)] IReadOnlyList<string> Names);
public sealed record SetSubmissionDestinationRequest(Guid SubmissionRecipientId, [Required, MinLength(1), MaxLength(20)] IReadOnlyList<Guid> SubmissionDepartmentIds);
public sealed record ReportResponsibilitiesRequest(Guid CoordinatingDivisionId, Guid SubmittingDivisionId, Guid PrimarySubmittingOfficerId, Guid? DeputySubmittingOfficerId);
public sealed record CreateCycleRequest(
    [Required, StringLength(80)] string PeriodKey, [Required, StringLength(150)] string PeriodLabel,
    DateOnly PeriodStart, DateOnly PeriodEnd,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyList<CreateObligationRequest> Obligations);
public sealed record ReportingCycleItem(Guid Id, string PeriodKey, string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd,
    IReadOnlyList<CycleObligationItem> Obligations);
public sealed record CycleObligationItem(string Name, string Type, Guid DivisionId, string DivisionName,
    Guid? PrimaryOfficerId, string? PrimaryOfficerName, Guid? DeputyOfficerId, string? DeputyOfficerName,
    string Recipient, DateTime DueAtUtc, bool ReceiptRequired, bool EvidenceRequired, Guid? ReceivingDivisionId = null,
    IReadOnlyList<ComponentContributorItem>? Contributors = null);
public sealed record CreateObligationRequest(
    [Required, StringLength(250)] string Name, string Type, Guid DivisionId, Guid PrimaryOfficerId,
    Guid? DeputyOfficerId, [Required, StringLength(4000)] string Recipient, DateTime DueAtUtc,
    bool ReceiptRequired, bool EvidenceRequired, Guid? ReceivingDivisionId = null);

public sealed record ComponentItem(
    Guid Id, Guid DefinitionVersionId, string Name, string? Description, bool IsRequired, int Sequence,
    Guid ProviderDivisionId, Guid? PrimaryOfficerId, Guid? DeputyOfficerId, Guid ReceivingDivisionId,
    bool ReceiptRequired, bool EvidenceRequired, int DeadlineOffsetDays, IReadOnlyList<ComponentContributorItem>? Contributors = null);

public sealed record SaveComponentRequest(
    [Required, StringLength(250)] string Name,
    [StringLength(1000)] string? Description,
    bool IsRequired,
    int Sequence,
    Guid ProviderDivisionId,
    Guid? PrimaryOfficerId,
    Guid? DeputyOfficerId,
    Guid ReceivingDivisionId,
    bool ReceiptRequired,
    bool EvidenceRequired,
    [Range(0, 365)] int DeadlineOffsetDays = 1, [MaxLength(200)] IReadOnlyList<ComponentContributorItem>? Contributors = null);
