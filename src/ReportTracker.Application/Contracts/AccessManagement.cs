using System.ComponentModel.DataAnnotations;

namespace ReportTracker.Application.Contracts;

public sealed record AccessPermission(string Code, string Name, string Group, string Description, bool GlobalOnly = false);
public sealed record AccessRole(Guid Id, string Code, string Name, string? Description, bool IsActive, bool IsSystem,
    IReadOnlyList<string> Permissions, string RowVersion);
public sealed record AccessReport(Guid Id, string Code, string Name);
public sealed record AccessCatalogDirectory(IReadOnlyList<AccessRole> Roles, IReadOnlyList<AccessPermission> Permissions,
    IReadOnlyList<LookupItem> Divisions, IReadOnlyList<AccessReport> Reports);
public sealed record RoleAssignmentRequest(Guid RoleId, [Required] string Scope, Guid? DivisionId = null, Guid? ReportDefinitionId = null);
public sealed record UserRoleAssignment(Guid RoleId, string Code, string Name, string Scope, Guid? DivisionId, Guid? ReportDefinitionId, bool IsRoleActive);
public sealed record UserAccessDirectory(Guid UserId, string User, Guid DivisionId, string RowVersion,
    IReadOnlyList<UserRoleAssignment> Assignments, IReadOnlyList<UserPermissionScope> EffectivePermissions);
public sealed record UserPermissionScope(string Code, string Scope, Guid? DivisionId, Guid? ReportDefinitionId, IReadOnlyList<string> Sources);
public sealed record SaveUserAccessRequest([Required, MaxLength(50)] IReadOnlyList<RoleAssignmentRequest> Assignments, [Required] string RowVersion);
public sealed record CreateAccessRoleRequest([Required, StringLength(64)] string Code, [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description, [Required, MaxLength(40)] IReadOnlyList<string> Permissions);
public sealed record UpdateAccessRoleRequest([Required, StringLength(100)] string Name, [StringLength(500)] string? Description,
    bool IsActive, [Required, MaxLength(40)] IReadOnlyList<string> Permissions, [Required] string RowVersion);
public sealed record UpdateAccessUserRequest([Required, StringLength(150)] string DisplayName,
    [Required, EmailAddress, StringLength(254)] string Email, [Phone, StringLength(30)] string? MobileNumber,
    Guid DivisionId, [Required] string RowVersion);
