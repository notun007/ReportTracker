using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public static class AccessControlSeed
{
    // Explicit deployment/initial-setup operation. Never run this on ordinary requests:
    // administrators' later permission changes and revocations must remain effective.
    public static async Task<IReadOnlyDictionary<string,AppRole>> PrepareAsync(ReportTrackerDbContext db, bool migrateExisting, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToDictionaryAsync(x => x.Code, ct);
        foreach (var permission in AccessPermissions.Catalog)
            if (!permissions.ContainsKey(permission.Code))
            {
                var row = new Permission { Code = permission.Code, Name = permission.Name };
                permissions.Add(row.Code, row); db.Permissions.Add(row);
            }
            else permissions[permission.Code].Name = permission.Name;
        var roles = await db.Roles.ToListAsync(ct);
        var core = new Dictionary<string,AppRole>();
        var created = new HashSet<Guid>();
        var names = new Dictionary<string,string>
        {
            ["SYS_ADMIN"]="System Administrator", ["REPORT_ADMIN"]="Reporting Administrator",
            ["REPORT_PREPARER"]="Report Preparer", ["COMPONENT_RECEIVER"]="Component Receiver", ["FINAL_SUBMITTER"]="Final Submitter"
        };
        foreach (var (code,name) in names)
        {
            var row = roles.FirstOrDefault(x => x.Code == code) ?? roles.FirstOrDefault(x =>
                x.Name.Equals(name,StringComparison.OrdinalIgnoreCase) || code == "REPORT_ADMIN" && x.Name == "Report Coordinator");
            if (row is null) { row = new AppRole { Code=code, Name=name }; roles.Add(row); db.Roles.Add(row); created.Add(row.Id); }
            row.Code=code; row.Name=name; row.IsSystem=true; row.IsActive=true;
            core.Add(code,row);
        }
        foreach (var role in roles.Where(x => string.IsNullOrWhiteSpace(x.Code))) role.Code="ROLE_"+role.Id.ToString("N").ToUpperInvariant();
        var mappings = await db.RolePermissions.ToListAsync(ct);
        var pairs = mappings.Select(x => (x.RoleId,x.PermissionId)).ToHashSet();
        void Map(AppRole role,string code)
        {
            if (pairs.Add((role.Id,permissions[code].Id))) db.RolePermissions.Add(new RolePermission { RoleId=role.Id, PermissionId=permissions[code].Id });
        }
        foreach (var (code,codes) in AccessPermissions.Defaults)
            if (!migrateExisting || created.Contains(core[code].Id) || code is "REPORT_PREPARER" or "COMPONENT_RECEIVER" or "FINAL_SUBMITTER")
                foreach (var permission in codes) Map(core[code],permission);
        if (!migrateExisting) return core;
        // Preserve the old broad grants with the same division/report scope.
        foreach (var role in roles)
        {
            if (mappings.Any(x => x.RoleId == role.Id && x.PermissionId == permissions[AccessPermissions.Reference].Id))
                foreach (var code in new[] { AccessPermissions.Users,AccessPermissions.Divisions,AccessPermissions.Roles }) Map(role,code);
            if (mappings.Any(x => x.RoleId == role.Id && x.PermissionId == permissions[AccessPermissions.Reports].Id))
                foreach (var code in AccessPermissions.Defaults["REPORT_ADMIN"].Where(x => x != AccessPermissions.LegacyRead)) Map(role,code);
            if (mappings.Any(x => x.RoleId == role.Id && x.PermissionId == permissions[AccessPermissions.LegacyRecord].Id)) Map(role,AccessPermissions.Upload);
        }
        var userIds = (await db.Users.Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var existing = await db.UserRoles.ToListAsync(ct);
        void Assign(Guid? userId,Guid reportId,string code)
        {
            if (!userId.HasValue || !userIds.Contains(userId.Value)) return;
            var roleId=core[code].Id;
            if (existing.Any(x => x.UserId == userId && x.RoleId == roleId && x.DivisionId == null && x.ReportDefinitionId == reportId)) return;
            var row=new UserRole { UserId=userId.Value,RoleId=roleId,ReportDefinitionId=reportId };
            existing.Add(row); db.UserRoles.Add(row);
        }
        var versions=await db.DefinitionVersions.AsNoTracking().ToDictionaryAsync(x => x.Id,x => x.ReportDefinitionId,ct);
        foreach (var component in await db.ComponentDefinitions.AsNoTracking().ToListAsync(ct))
        {
            if (!versions.TryGetValue(component.DefinitionVersionId,out var reportId)) continue;
            Assign(component.PrimaryAssigneeId,reportId,"REPORT_PREPARER"); Assign(component.AlternateAssigneeId,reportId,"REPORT_PREPARER");
            foreach (var member in ComponentValidation.Contributors(component.ContributorsJson,component.PrimaryAssigneeId,component.AlternateAssigneeId))
                Assign(member.UserId,reportId,"REPORT_PREPARER");
        }
        var cycles=await db.ReportCycles.AsNoTracking().ToDictionaryAsync(x => x.Id,x => x.ReportDefinitionId,ct);
        foreach (var task in await db.TrackingObligations.AsNoTracking().ToListAsync(ct))
        {
            if (!cycles.TryGetValue(task.ReportCycleId,out var reportId)) continue;
            var code=task.Type == ObligationType.External ? "FINAL_SUBMITTER" : "REPORT_PREPARER";
            Assign(task.PrimaryAssigneeId,reportId,code); Assign(task.AlternateAssigneeId,reportId,code);
            foreach (var member in ComponentValidation.Contributors(task.ContributorsJson,task.PrimaryAssigneeId,task.AlternateAssigneeId)) Assign(member.UserId,reportId,code);
        }
        foreach (var row in await db.ResponsibilityAssignments.AsNoTracking().ToListAsync(ct))
            if (row.ResponsibilityType is "MainSender" or TrackingAccess.ComponentReceiverResponsibility)
            {
                var code=row.ResponsibilityType == "MainSender" ? "FINAL_SUBMITTER" : "COMPONENT_RECEIVER";
                Assign(row.PrimaryAssigneeId,row.ReportDefinitionId,code); Assign(row.AlternateAssigneeId,row.ReportDefinitionId,code);
            }
        return core;
    }
}
