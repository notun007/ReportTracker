using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public sealed class TrackingAccess(ReportTrackerDbContext db, ICurrentUser currentUser)
{
    public const string ComponentReceiverResponsibility = "ComponentReceiver";
    public Guid UserId => currentUser.UserId;

    public async Task EnsureActiveAsync(CancellationToken ct)
    {
        if (UserId == Guid.Empty || !await db.Users.AnyAsync(x => x.Id == UserId && x.IsActive, ct))
            throw new UnauthorizedAccessException("An active user account is required.");
    }

    public IQueryable<UserRole> Grants(string permission) => GrantsAny(permission);
    public IQueryable<UserRole> GrantsAny(params string[] permissions) =>
        from assignment in db.UserRoles
        join role in db.Roles on assignment.RoleId equals role.Id
        join user in db.Users on assignment.UserId equals user.Id
        join mapping in db.RolePermissions on assignment.RoleId equals mapping.RoleId
        join grant in db.Permissions on mapping.PermissionId equals grant.Id
        where assignment.UserId == UserId && assignment.IsActive && role.IsActive && user.IsActive && permissions.Contains(grant.Code)
        select assignment;
    public IQueryable<UserRole> ConfigurationGrants() => GrantsAny(AccessPermissions.Configuration);

    public async Task<bool> HasAsync(string permission, Guid? divisionId, Guid? reportId, CancellationToken ct) =>
        await Grants(permission).AnyAsync(x => (!x.DivisionId.HasValue || x.DivisionId == divisionId) &&
                                               (!x.ReportDefinitionId.HasValue || x.ReportDefinitionId == reportId), ct);

    public IQueryable<TrackingObligation> AssignedObligations(bool includeContributors = true)
    {
        var active = db.Users.Where(x => x.Id == UserId && x.IsActive);
        var direct = db.TrackingObligations.Where(x => active.Any() &&
            (x.PrimaryAssigneeId == UserId || x.AlternateAssigneeId == UserId));
        if (!includeContributors) return direct;
        var componentIds = ComponentContributorAccess.AssignedComponents(db, UserId).Select(x => x.Id);
        return db.TrackingObligations.Where(x => active.Any() &&
            (x.PrimaryAssigneeId == UserId || x.AlternateAssigneeId == UserId || componentIds.Contains(x.Id)));
    }

    public IQueryable<ComponentDefinition> ManageableComponentTeams()
    {
        var divisions = db.Users.Where(x => x.Id == UserId && x.IsActive).Select(x => x.DivisionId);
        var grants = Grants(AccessPermissions.Teams);
        return from component in db.ComponentDefinitions
               join version in db.DefinitionVersions on component.DefinitionVersionId equals version.Id
               join report in db.ReportDefinitions on version.ReportDefinitionId equals report.Id
               join division in db.Divisions on component.ProviderDivisionId equals division.Id
               where report.IsActive && division.IsActive &&
                   !db.DefinitionVersions.Any(v => v.ReportDefinitionId == report.Id && v.VersionNumber > version.VersionNumber) &&
                   (grants.Any(g => !g.DivisionId.HasValue && !g.ReportDefinitionId.HasValue) ||
                    divisions.Contains(component.ProviderDivisionId) && grants.Any(g =>
                        (!g.DivisionId.HasValue || g.DivisionId == component.ProviderDivisionId) &&
                        (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == report.Id)))
               select component;
    }

    public IQueryable<TrackingObligation> AuthorizedObligations(bool writing = false)
    {
        var broad = Grants(writing ? AccessPermissions.LegacyRecord : AccessPermissions.LegacyRead);
        var monitors = GrantsAny(AccessPermissions.Monitor, AccessPermissions.EvidenceReview, AccessPermissions.Extensions);
        var preparing = writing ? Grants(AccessPermissions.ComponentSubmit) : GrantsAny(AccessPermissions.ComponentRead, AccessPermissions.ComponentSubmit);
        var submitting = writing ? Grants(AccessPermissions.ReportSubmit) : GrantsAny(AccessPermissions.ReportRead, AccessPermissions.ReportSubmit);
        var active = db.Users.Where(x => x.Id == UserId && x.IsActive);
        var incomingIds = AssignedReceivingObligations().Select(x => x.Id);
        var assignedIds = AssignedObligations(includeContributors: !writing).Select(x => x.Id);
        return db.TrackingObligations.Where(x => active.Any() && (
            (assignedIds.Contains(x.Id) && (
                (x.Type == ObligationType.External && submitting.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.AccountableDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId))) ||
                (x.Type != ObligationType.External && preparing.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.AccountableDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId))))) ||
            broad.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.AccountableDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId)) ||
            (!writing && (incomingIds.Contains(x.Id) || monitors.Any(g =>
                (!g.DivisionId.HasValue || g.DivisionId == x.ReportCycle.ReportDefinition.OwnerDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId))))));
    }

    public IQueryable<TrackingObligation> AssignedReceivingObligations()
    {
        var now = DateTime.UtcNow;
        var permissions = Grants(AccessPermissions.Receive);
        var divisions = db.Users.Where(x => x.Id == UserId && x.IsActive).Select(x => x.DivisionId);
        var assignments = db.ResponsibilityAssignments.Where(x => x.ResponsibilityType == ComponentReceiverResponsibility &&
            (x.PrimaryAssigneeId == UserId || x.AlternateAssigneeId == UserId) && divisions.Contains(x.DivisionId) &&
            x.EffectiveFromUtc <= now && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now));
        return db.TrackingObligations.Where(x => x.Type == ObligationType.Internal && x.ReceivingDivisionId.HasValue &&
            assignments.Any(a => a.ReportDefinitionId == x.ReportCycle.ReportDefinitionId && a.DivisionId == x.ReceivingDivisionId) &&
            permissions.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.ReceivingDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId)));
    }

    public IQueryable<TrackingObligation> AuthorizedReceiptObligations()
    {
        var active = db.Users.Where(x => x.Id == UserId && x.IsActive);
        var incomingIds = AssignedReceivingObligations().Select(x => x.Id);
        var preparingIds = AssignedObligations().Select(x => x.Id);
        var coordinators = Grants(AccessPermissions.Responsibilities);
        return db.TrackingObligations.Where(x => active.Any() && x.PrimaryAssigneeId != UserId && x.AlternateAssigneeId != UserId &&
            !x.SendingEvents.Any(s => !s.IsReversed && s.ActingUserId == UserId) &&
            ((incomingIds.Contains(x.Id) && !preparingIds.Contains(x.Id)) ||
             (x.Type == ReportTracker.Domain.Enums.ObligationType.External && coordinators.Any(g =>
                (!g.DivisionId.HasValue || g.DivisionId == x.ReportCycle.ReportDefinition.OwnerDivisionId) &&
                (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId)))));
    }

    public async Task RequireAsync(string permission, Guid? divisionId, Guid? reportId, CancellationToken ct)
    {
        await EnsureActiveAsync(ct);
        if (!await HasAsync(permission, divisionId, reportId, ct)) throw new UnauthorizedAccessException("You do not have permission for this action.");
    }
}
