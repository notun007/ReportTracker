using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController,Authorize,Route("api/access")]
public sealed class AccessManagementController(ReportTrackerDbContext db,TrackingAccess access) : ControllerBase
{
    private AccessAdministration Administration => new(db,access);
    [HttpGet("catalog")]
    public async Task<ActionResult<AccessCatalogDirectory>> Catalog(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.HasAsync(AccessPermissions.Roles,null,null,ct) && !await access.HasAsync(AccessPermissions.Users,null,null,ct)) return Forbid();
        var mappings=await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                            select new { mapping.RoleId,permission.Code }).ToListAsync(ct);
        var roles=await db.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return Ok(new AccessCatalogDirectory(roles.Select(x => new AccessRole(x.Id,x.Code,x.Name,x.Description,x.IsActive,x.IsSystem,
            mappings.Where(p => p.RoleId == x.Id).Select(p => p.Code).Order().ToArray(),Convert.ToBase64String(x.RowVersion))).ToArray(),
            AccessPermissions.Catalog,await db.Divisions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItem(x.Id,x.Name)).ToListAsync(ct),
            await db.ReportDefinitions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new AccessReport(x.Id,x.Code,x.Name)).ToListAsync(ct)));
    }
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(CreateAccessRoleRequest request,CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        var code=request.Code.Trim().ToUpperInvariant(); var name=request.Name.Trim();
        if (!Regex.IsMatch(code,"^[A-Z][A-Z0-9_]{2,63}$") || string.IsNullOrWhiteSpace(name)) throw new TrackingValidationException("Use a role code of 3–64 uppercase letters, numbers or underscores, starting with a letter, and enter a role name.");
        if (await db.Roles.AnyAsync(x => x.Code == code || x.Name.ToUpper() == name.ToUpper(),ct)) throw new TrackingConflictException("A role with this code or name already exists.");
        var permissions=AccessAdministration.ValidatePermissions(request.Permissions);
        var role=new AppRole { Code=code,Name=name,Description=request.Description?.Trim() };
        db.Roles.Add(role); await AddPermissions(role.Id,permissions,ct);
        Audit(role.Id,"RoleCreated",new { role.Code,role.Name,Permissions=permissions }); await db.SaveChangesAsync(ct);
        return Created($"/api/access/roles/{role.Id}",new { role.Id });
    }
    [HttpPut("roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid roleId,UpdateAccessRoleRequest request,CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        await using var transaction=await Administration.BeginMutationAsync(ct);
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        var role=await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId,ct); if (role is null) return NotFound();
        AccessAdministration.CheckVersion(role,request.RowVersion);
        if (role.IsSystem && !request.IsActive) throw new TrackingValidationException("Core roles cannot be deactivated. Manage their user assignments instead.");
        var name=request.Name.Trim(); if (name.Length == 0) throw new TrackingValidationException("Enter a role name.");
        if (await db.Roles.AnyAsync(x => x.Id != roleId && x.Name.ToUpper() == name.ToUpper(),ct)) throw new TrackingConflictException("A role with this name already exists.");
        var permissions=AccessAdministration.ValidatePermissions(request.Permissions);
        if (permissions.Any(code => AccessPermissions.Catalog.Any(x => x.Code == code && x.GlobalOnly)) &&
            await db.UserRoles.AnyAsync(x => x.RoleId == roleId && x.IsActive && (x.DivisionId.HasValue || x.ReportDefinitionId.HasValue),ct))
            throw new TrackingValidationException("This role has scoped assignments. Review those scopes before adding global administration permissions.");
        await Administration.EnsureAdministratorRemainsAsync(ct,roleId:roleId,permissions:permissions,roleActive:request.IsActive);
        var before=new { role.Name,role.Description,role.IsActive,Permissions=await RoleCodes(roleId,ct) };
        var existingMappings=await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(ct);
        var selectedPermissions=await db.Permissions.Where(x => permissions.Contains(x.Code)).ToListAsync(ct);
        db.RolePermissions.RemoveRange(existingMappings.Where(x => !selectedPermissions.Any(p => p.Id == x.PermissionId)));
        role.Name=name; role.Description=request.Description?.Trim(); role.IsActive=request.IsActive;
        role.UpdatedAtUtc=DateTime.UtcNow;
        db.RolePermissions.AddRange(selectedPermissions.Where(p => !existingMappings.Any(x => x.PermissionId == p.Id))
            .Select(p => new RolePermission { RoleId=roleId,PermissionId=p.Id }));
        Audit(roleId,"RoleUpdated",new { Previous=before,role.Code,role.Name,role.Description,role.IsActive,Permissions=permissions });
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }
    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<UserAccessDirectory>> UserAccess(Guid userId,CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId,ct); if (user is null) return NotFound();
        var roles=await db.Roles.AsNoTracking().ToDictionaryAsync(x => x.Id,ct);
        var grants=await db.UserRoles.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).ToListAsync(ct);
        var mappings=await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                            select new { mapping.RoleId,permission.Code }).ToListAsync(ct);
        var effective=grants.Where(g => user.IsActive && roles[g.RoleId].IsActive)
            .SelectMany(g => mappings.Where(p => p.RoleId == g.RoleId).Select(p => new { p.Code,g.DivisionId,g.ReportDefinitionId,Source=roles[g.RoleId].Name }))
            .GroupBy(x => (x.Code,x.DivisionId,x.ReportDefinitionId))
            .Select(g => new UserPermissionScope(g.Key.Code,AccessAdministration.Scope(g.Key.DivisionId,g.Key.ReportDefinitionId),g.Key.DivisionId,g.Key.ReportDefinitionId,g.Select(x => x.Source).Distinct().Order().ToArray()))
            .OrderBy(x => x.Code).ToArray();
        return Ok(new UserAccessDirectory(user.Id,user.DisplayName,user.DivisionId,Convert.ToBase64String(user.RowVersion),
            grants.Select(g => new UserRoleAssignment(g.RoleId,roles[g.RoleId].Code,roles[g.RoleId].Name,AccessAdministration.Scope(g.DivisionId,g.ReportDefinitionId),g.DivisionId,g.ReportDefinitionId,roles[g.RoleId].IsActive)).ToArray(),effective));
    }
    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> SaveUserAccess(Guid userId,SaveUserAccessRequest request,CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        await using var transaction=await Administration.BeginMutationAsync(ct);
        await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
        var user=await db.Users.SingleOrDefaultAsync(x => x.Id == userId,ct); if (user is null) return NotFound();
        AccessAdministration.CheckVersion(user,request.RowVersion);
        await Administration.ValidateAssignmentsAsync(request.Assignments,ct);
        await Administration.EnsureAdministratorRemainsAsync(ct,userId:userId,assignments:request.Assignments);
        await Administration.ReplaceAssignmentsAsync(user,request.Assignments,ct);
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }
    private async Task<string[]> RoleCodes(Guid roleId,CancellationToken ct) => await (from mapping in db.RolePermissions
        join permission in db.Permissions on mapping.PermissionId equals permission.Id where mapping.RoleId == roleId select permission.Code).ToArrayAsync(ct);
    private async Task AddPermissions(Guid roleId,string[] codes,CancellationToken ct)
    {
        var permissions=await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Length) throw new TrackingValidationException("The permission catalogue is not initialized. Apply the access upgrade first.");
        db.RolePermissions.AddRange(permissions.Select(x => new RolePermission { RoleId=roleId,PermissionId=x.Id }));
    }
    private void Audit(Guid roleId,string action,object data) => db.AuditEvents.Add(new AuditEvent { EntityType=nameof(AppRole),EntityId=roleId,ActorUserId=access.UserId,Action=action,DataJson=JsonSerializer.Serialize(data) });
}
