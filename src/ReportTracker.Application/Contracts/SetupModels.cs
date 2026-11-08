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
public sealed record UserItem(Guid Id, string EmployeeId, string DisplayName, string Email, string? MobileNumber, Guid DivisionId, bool IsActive, IReadOnlyList<string> Roles);
public sealed record CreateUserRequest(
    [Required, StringLength(40)] string EmployeeId, [Required, StringLength(150)] string DisplayName,
    [Required, EmailAddress, StringLength(254)] string Email, [Phone, StringLength(30)] string? MobileNumber, Guid DivisionId,
    [Required, StringLength(128, MinimumLength = 12)] string Password, bool IsCoordinator);
public sealed record CreateDivisionRequest([Required, StringLength(40)] string Code, [Required, StringLength(150)] string Name);
public sealed record CreateReportRequest(
    [Required, StringLength(40)] string Code, [Required, StringLength(150)] string Name,
    [StringLength(1000)] string? Description, Guid OwnerDivisionId,
    [Required, StringLength(250)] string BbRecipient, [StringLength(250)] string? InstructionReference,
    string Frequency, TimeOnly? DueTime = null, int ComponentDeadlineOffsetDays = 1);
public sealed record ReportItem(Guid Id, string Code, string Name, Guid OwnerDivisionId, string BbRecipient, string Frequency, int CycleCount);
public sealed record CreateCycleRequest(
    [Required, StringLength(80)] string PeriodKey, [Required, StringLength(150)] string PeriodLabel,
    DateOnly PeriodStart, DateOnly PeriodEnd,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyList<CreateObligationRequest> Obligations);
public sealed record CreateObligationRequest(
    [Required, StringLength(250)] string Name, string Type, Guid DivisionId, Guid PrimaryOfficerId,
    Guid? DeputyOfficerId, [Required, StringLength(250)] string Recipient, DateTime DueAtUtc,
    bool ReceiptRequired, bool EvidenceRequired);

public sealed record ComponentItem(
    Guid Id, Guid DefinitionVersionId, string Name, string? Description, bool IsRequired, int Sequence,
    Guid ProviderDivisionId, Guid? PrimaryOfficerId, Guid? DeputyOfficerId, Guid ReceivingDivisionId,
    bool ReceiptRequired, bool EvidenceRequired, int DeadlineOffsetDays);

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
    [Range(0, 365)] int DeadlineOffsetDays);
