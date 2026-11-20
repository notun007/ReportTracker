using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Common;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public sealed class AccessAdministration(ReportTrackerDbContext db, TrackingAccess access)
{
    public static string Scope(Guid? divisionId,Guid? reportId) => divisionId.HasValue
        ? reportId.HasValue ? "DivisionReport" : "Division" : reportId.HasValue ? "Report" : "Global";
    public static void CheckVersion(Entity row,string version)
    {
        byte[] bytes;
        try { bytes=Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { throw new TrackingValidationException("Refresh the record and try again."); }
        if (bytes.Length != 8) throw new TrackingValidationException("Refresh the record and try again.");
        if (!bytes.SequenceEqual(row.RowVersion)) throw new TrackingConflictException("This record changed. Refresh and try again.");
    }
    public async Task<IDbContextTransaction?> BeginMutationAsync(CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction=await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsOracle()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"rt_user_roles\" IN EXCLUSIVE MODE",ct);
        return transaction;
    }
    public static string[] ValidatePermissions(IReadOnlyList<string>? codes)
    {
        if (codes is null || codes.Count > 40 || codes.Distinct().Count() != codes.Count ||
            codes.Any(code => !AccessPermissions.Catalog.Any(x => x.Code == code)))
            throw new TrackingValidationException("Choose distinct permissions from the implemented permission catalogue.");
        return codes.ToArray();
    }
    public async Task ValidateAssignmentsAsync(IReadOnlyList<RoleAssignmentRequest>? assignments,CancellationToken ct)
    {
        if (assignments is null || assignments.Count > 50 || assignments.Any(x => x is null) ||
            assignments.Select(x => (x.RoleId,x.DivisionId,x.ReportDefinitionId)).Distinct().Count() != assignments.Count)
            throw new TrackingValidationException("Choose up to 50 distinct role and scope assignments.");
        var roles=await db.Roles.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.Id,ct);
        var globalCodes=AccessPermissions.Catalog.Where(x => x.GlobalOnly).Select(x => x.Code).ToArray();
        var globalRoles=await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                              where globalCodes.Contains(permission.Code) select mapping.RoleId).ToListAsync(ct);
        foreach (var assignment in assignments)
        {
            if (!roles.ContainsKey(assignment.RoleId)) throw new TrackingValidationException("Choose active roles.");
            var valid=assignment.Scope switch
            {
                "Global" => !assignment.DivisionId.HasValue && !assignment.ReportDefinitionId.HasValue,
                "Division" => assignment.DivisionId.HasValue && !assignment.ReportDefinitionId.HasValue,
                "Report" => !assignment.DivisionId.HasValue && assignment.ReportDefinitionId.HasValue,
                "DivisionReport" => assignment.DivisionId.HasValue && assignment.ReportDefinitionId.HasValue,
                _ => false
            };
            if (!valid) throw new TrackingValidationException("Choose an explicit scope and supply its division or report. Empty scope never grants global access.");
            if (assignment.Scope != "Global" && globalRoles.Contains(assignment.RoleId))
                throw new TrackingValidationException("Roles with administration permissions require an explicit Global scope.");
            if (assignment.DivisionId.HasValue && !await db.Divisions.AnyAsync(x => x.Id == assignment.DivisionId && x.IsActive,ct))
                throw new TrackingValidationException("Choose an active division for each scoped role.");
            if (assignment.ReportDefinitionId.HasValue && !await db.ReportDefinitions.AnyAsync(x => x.Id == assignment.ReportDefinitionId && x.IsActive,ct))
                throw new TrackingValidationException("Choose an active report for each scoped role.");
        }
    }
    public async Task EnsureAdministratorRemainsAsync(CancellationToken ct,Guid? userId=null,IReadOnlyList<RoleAssignmentRequest>? assignments=null,
        Guid? roleId=null,IReadOnlyList<string>? permissions=null,bool roleActive=true,Guid? deactivatedUserId=null)
    {
        var users=(await db.Users.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        if (deactivatedUserId.HasValue) users.Remove(deactivatedUserId.Value);
        var activeRoles=(await db.Roles.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var managerRoles=(await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                                where permission.Code == AccessPermissions.Roles select mapping.RoleId).ToListAsync(ct)).ToHashSet();
        managerRoles.IntersectWith(activeRoles);
        if (roleId.HasValue)
        {
            managerRoles.Remove(roleId.Value);
            if (roleActive && permissions?.Contains(AccessPermissions.Roles) == true) managerRoles.Add(roleId.Value);
        }
        var grants=await db.UserRoles.AsNoTracking().Where(x => x.IsActive && !x.DivisionId.HasValue && !x.ReportDefinitionId.HasValue).ToListAsync(ct);
        var survives=grants.Any(x => (!userId.HasValue || x.UserId != userId) && users.Contains(x.UserId) && managerRoles.Contains(x.RoleId));
        survives |= userId.HasValue && users.Contains(userId.Value) && assignments?.Any(x => x.Scope == "Global" && managerRoles.Contains(x.RoleId)) == true;
        if (!survives) throw new TrackingConflictException("Keep at least one active user with Global permission to manage roles and access.");
    }
    public async Task ReplaceAssignmentsAsync(AppUser user,IReadOnlyList<RoleAssignmentRequest> assignments,CancellationToken ct)
    {
        var existing=await db.UserRoles.Where(x => x.UserId == user.Id).ToListAsync(ct);
        var previous=existing.Where(x => x.IsActive).Select(x => new { x.RoleId,x.DivisionId,x.ReportDefinitionId }).ToArray();
        foreach (var row in existing) row.IsActive=false;
        foreach (var assignment in assignments)
        {
            var row=existing.FirstOrDefault(x => x.RoleId == assignment.RoleId && x.DivisionId == assignment.DivisionId && x.ReportDefinitionId == assignment.ReportDefinitionId);
            if (row is null) db.UserRoles.Add(new UserRole { UserId=user.Id,RoleId=assignment.RoleId,DivisionId=assignment.DivisionId,ReportDefinitionId=assignment.ReportDefinitionId });
            else row.IsActive=true;
        }
        user.UpdatedAtUtc=DateTime.UtcNow;
        db.AuditEvents.Add(new AuditEvent { EntityType=nameof(AppUser),EntityId=user.Id,ActorUserId=access.UserId,Action="UserAccessChanged",
            DataJson=JsonSerializer.Serialize(new { Previous=previous,Assignments=assignments }) });
    }
}
