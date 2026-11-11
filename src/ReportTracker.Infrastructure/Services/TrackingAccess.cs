using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public sealed class TrackingAccess(ReportTrackerDbContext db, ICurrentUser currentUser)
{
    public Guid UserId => currentUser.UserId;

    public async Task EnsureActiveAsync(CancellationToken ct)
    {
        if (UserId == Guid.Empty || !await db.Users.AnyAsync(x => x.Id == UserId && x.IsActive, ct))
            throw new UnauthorizedAccessException("An active user account is required.");
    }

    public IQueryable<UserRole> Grants(string permission) =>
        from assignment in db.UserRoles
        join mapping in db.RolePermissions on assignment.RoleId equals mapping.RoleId
        join grant in db.Permissions on mapping.PermissionId equals grant.Id
        where assignment.UserId == UserId && grant.Code == permission
        select assignment;

    public async Task<bool> HasAsync(string permission, Guid? divisionId, Guid? reportId, CancellationToken ct) =>
        await Grants(permission).AnyAsync(x => (!x.DivisionId.HasValue || x.DivisionId == divisionId) &&
                                               (!x.ReportDefinitionId.HasValue || x.ReportDefinitionId == reportId), ct);

    public IQueryable<TrackingObligation> AuthorizedObligations(bool writing = false)
    {
        var grants = Grants(writing ? "tracking.record" : "tracking.read");
        var reviewers = Grants("reports.manage");
        var receivingDivisions = db.Users.Where(x => x.Id == UserId && x.IsActive).Select(x => x.DivisionId);
        return db.TrackingObligations.Where(x => x.PrimaryOfficerId == UserId || x.DeputyOfficerId == UserId ||
            grants.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.AccountableDivisionId) &&
                            (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId)) ||
            (!writing && ((x.ReceivingDivisionId.HasValue && receivingDivisions.Contains(x.ReceivingDivisionId.Value)) ||
                reviewers.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.ReportCycle.ReportDefinition.OwnerDivisionId) &&
                    (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.ReportCycle.ReportDefinitionId)))));
    }

    public IQueryable<TrackingObligation> AuthorizedReceiptObligations()
    {
        var receivingDivisions = db.Users.Where(x => x.Id == UserId && x.IsActive).Select(x => x.DivisionId);
        var coordinators = Grants("reports.manage");
        return db.TrackingObligations.Where(x => x.PrimaryOfficerId != UserId && x.DeputyOfficerId != UserId &&
            !x.SendingEvents.Any(s => !s.IsReversed && s.ActingUserId == UserId) &&
            ((x.Type == ReportTracker.Domain.Enums.ObligationType.Internal && x.ReceivingDivisionId.HasValue &&
                receivingDivisions.Contains(x.ReceivingDivisionId.Value)) ||
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
