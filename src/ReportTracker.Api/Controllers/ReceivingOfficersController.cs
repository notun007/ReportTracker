using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/report-definitions/{reportId:guid}/receiving-officers")]
public sealed class ReceivingOfficersController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReceivingOfficerDirectory>> Get(Guid reportId, Guid divisionId, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        var division = await ValidateDivision(report, divisionId, ct);
        var now = DateTime.UtcNow;
        var assignments = await ActiveAssignments(reportId, divisionId, now).ToListAsync(ct);
        var selected = assignments.SelectMany(x => new Guid?[] { x.PrimaryAssigneeId, x.AlternateAssigneeId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var officers = await db.Users.AsNoTracking().Where(x => x.IsActive && x.DivisionId == divisionId)
            .OrderBy(x => x.DisplayName).Select(x => new LookupItem(x.Id, x.DisplayName)).ToListAsync(ct);
        return Ok(new ReceivingOfficerDirectory(reportId, divisionId, division.Name, officers, selected, Convert.ToBase64String(report.RowVersion)));
    }

    [HttpPut]
    public async Task<IActionResult> Set(Guid reportId, SetReceivingOfficersRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        await ValidateDivision(report, request.DivisionId, ct);
        if (request.OfficerIds is null || request.OfficerIds.Count > 50 || request.OfficerIds.Distinct().Count() != request.OfficerIds.Count)
            throw new TrackingValidationException("Choose up to 50 different receiving officers.");
        var ids = request.OfficerIds.ToArray();
        if (await db.Users.CountAsync(x => ids.Contains(x.Id) && x.IsActive && x.DivisionId == request.DivisionId, ct) != ids.Length)
            throw new TrackingValidationException("Receiving officers must be active members of the selected receiving division.");
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion ?? ""); }
        catch (FormatException) { throw new TrackingValidationException("Refresh receiving officers and try again."); }
        if (version.Length != 8) throw new TrackingValidationException("Refresh receiving officers and try again.");
        db.Entry(report).Property(x => x.RowVersion).OriginalValue = version;
        var now = DateTime.UtcNow;
        var previous = await ActiveAssignments(reportId, request.DivisionId, now).ToListAsync(ct);
        foreach (var assignment in previous) assignment.EffectiveToUtc = now;
        foreach (var id in ids)
            db.ResponsibilityAssignments.Add(new ResponsibilityAssignment
            {
                ReportDefinitionId = reportId, DivisionId = request.DivisionId, PrimaryAssigneeId = id,
                ResponsibilityType = TrackingAccess.ComponentReceiverResponsibility, EffectiveFromUtc = now
            });
        // Serialize concurrent edits and preserve the old assignments and audit trail.
        report.UpdatedAtUtc = now;
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(ReportDefinition), EntityId = reportId, ActorUserId = access.UserId,
            Action = "ReceivingOfficersAssigned", DataJson = JsonSerializer.Serialize(new
            {
                request.DivisionId, PreviousOfficerIds = previous.Select(x => x.PrimaryAssigneeId), OfficerIds = ids
            })
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<ResponsibilityAssignment> ActiveAssignments(Guid reportId, Guid divisionId, DateTime now) =>
        db.ResponsibilityAssignments.Where(x => x.ReportDefinitionId == reportId && x.DivisionId == divisionId &&
            x.ResponsibilityType == TrackingAccess.ComponentReceiverResponsibility && x.EffectiveFromUtc <= now &&
            (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now));

    private async Task<Division> ValidateDivision(ReportDefinition report, Guid divisionId, CancellationToken ct)
    {
        var division = await db.Divisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == divisionId && x.IsActive, ct);
        var routed = report.ReceivingDivisionId == divisionId ||
            await db.ComponentDefinitions.AnyAsync(x => x.DefinitionVersionId != Guid.Empty && x.ReceivingDivisionId == divisionId &&
                db.DefinitionVersions.Any(v => v.Id == x.DefinitionVersionId && v.ReportDefinitionId == report.Id), ct) ||
            await db.TrackingObligations.AnyAsync(x => x.ReportCycle.ReportDefinitionId == report.Id && x.ReceivingDivisionId == divisionId, ct);
        if (division is null || !routed) throw new TrackingValidationException("Choose a receiving division used by this report.");
        return division;
    }
}
