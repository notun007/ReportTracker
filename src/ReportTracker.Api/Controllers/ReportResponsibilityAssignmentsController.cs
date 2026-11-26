using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/report-definitions/{reportId:guid}/responsibility-assignments")]
public sealed class ReportResponsibilityAssignmentsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    public const string ControllerResponsibility = "ReportController";
    private const string FinalSubmissionResponsibility = "MainSender";

    [HttpGet]
    public async Task<ActionResult<ReportResponsibilityAssignments>> Get(Guid reportId, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        var now = DateTime.UtcNow;
        var assignments = await Current(reportId, now).AsNoTracking().OrderByDescending(x => x.EffectiveFromUtc).ThenBy(x => x.Id).ToListAsync(ct);
        var divisions = await db.Divisions.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var ids = new Guid?[] { report.OwnerDivisionId, report.ReceivingDivisionId, report.SubmittingDivisionId };
        var officers = await db.Users.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.DivisionId))
            .OrderBy(x => x.DisplayName).ToListAsync(ct);

        ResponsibilitySection Section(string type, Guid? divisionId)
        {
            var rows = assignments.Where(x => x.ResponsibilityType == type && x.DivisionId == divisionId).ToList();
            var first = rows.FirstOrDefault();
            // The legacy receiving editor stores individual receivers. Prefill up to two,
            // but never silently discard a larger existing receiving team on save.
            var people = rows.SelectMany(x => new Guid?[] { x.PrimaryAssigneeId, x.AlternateAssigneeId })
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            var deputy = type == TrackingAccess.ComponentReceiverResponsibility ? people.Skip(1).Select(x => (Guid?)x).FirstOrDefault() : first?.AlternateAssigneeId;
            return new(divisionId, divisionId.HasValue ? divisions.GetValueOrDefault(divisionId.Value, "Inactive division") : "Not configured",
                officers.Where(x => x.DivisionId == divisionId).Select(x => new LookupItem(x.Id, x.DisplayName)).ToList(),
                first?.PrimaryAssigneeId, deputy);
        }

        var receiverCount = assignments.Where(x => x.ResponsibilityType == TrackingAccess.ComponentReceiverResponsibility && x.DivisionId == report.ReceivingDivisionId)
            .SelectMany(x => new Guid?[] { x.PrimaryAssigneeId, x.AlternateAssigneeId }).Where(x => x.HasValue).Distinct().Count();
        return Ok(new ReportResponsibilityAssignments(reportId, report.Name, Convert.ToBase64String(report.RowVersion),
            Section(ControllerResponsibility, report.OwnerDivisionId), Section(TrackingAccess.ComponentReceiverResponsibility, report.ReceivingDivisionId),
            Section(FinalSubmissionResponsibility, report.SubmittingDivisionId), receiverCount));
    }

    [HttpPut]
    public async Task<IActionResult> Save(Guid reportId, SaveReportResponsibilityAssignmentsRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        await ValidatePair("Report Controller", report.OwnerDivisionId, request.Controller, ct);
        await ValidatePair("Report Receiver", report.ReceivingDivisionId, request.Receiver, ct);
        await ValidatePair("Final Submission", report.SubmittingDivisionId, request.FinalSubmission, ct);
        MarkAssignmentChange(report, request.RowVersion);
        var now = DateTime.UtcNow;
        var current = await Current(reportId, now).ToListAsync(ct);
        var receiverCount = current.Where(x => x.ResponsibilityType == TrackingAccess.ComponentReceiverResponsibility && x.DivisionId == report.ReceivingDivisionId)
            .SelectMany(x => new Guid?[] { x.PrimaryAssigneeId, x.AlternateAssigneeId }).Where(x => x.HasValue).Distinct().Count();
        if (receiverCount > 2)
            throw new TrackingConflictException("This report has more than two receiving officers. Review the existing Receiving officers list before replacing it with a primary and alternate.");

        var previous = current.Where(x => x.ResponsibilityType is ControllerResponsibility or FinalSubmissionResponsibility ||
            x.ResponsibilityType == TrackingAccess.ComponentReceiverResponsibility && x.DivisionId == report.ReceivingDivisionId).ToList();
        foreach (var assignment in previous) assignment.EffectiveToUtc = now;
        Add(ControllerResponsibility, report.OwnerDivisionId, request.Controller);
        Add(TrackingAccess.ComponentReceiverResponsibility, report.ReceivingDivisionId!.Value, request.Receiver);
        Add(FinalSubmissionResponsibility, report.SubmittingDivisionId!.Value, request.FinalSubmission);
        report.UpdatedAtUtc = now;
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(ReportDefinition), EntityId = reportId, ActorUserId = access.UserId, Action = "ReportResponsibilityAssignmentsChanged",
            DataJson = JsonSerializer.Serialize(new
            {
                Previous = previous.Select(x => new { x.ResponsibilityType, x.DivisionId, x.PrimaryAssigneeId, x.AlternateAssigneeId }),
                request.Controller, request.Receiver, request.FinalSubmission
            })
        });
        // Only report-level defaults change. Existing period tasks, divisions and roles are untouched.
        await db.SaveChangesAsync(ct);
        return NoContent();

        void Add(string type, Guid division, AssigneePair pair) => db.ResponsibilityAssignments.Add(new ResponsibilityAssignment
        {
            ReportDefinitionId = reportId, DivisionId = division, ResponsibilityType = type,
            PrimaryAssigneeId = pair.PrimaryAssigneeId, AlternateAssigneeId = pair.AlternateAssigneeId, EffectiveFromUtc = now
        });
    }

    [HttpDelete("{role}")]
    public async Task<IActionResult> Unassign(Guid reportId, string role,
        [FromBody] UnassignReportResponsibilityRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        var (type, divisionId) = role switch
        {
            "controller" => (ControllerResponsibility, (Guid?)report.OwnerDivisionId),
            "receiver" => (TrackingAccess.ComponentReceiverResponsibility, report.ReceivingDivisionId),
            "finalSubmission" => (FinalSubmissionResponsibility, report.SubmittingDivisionId),
            _ => throw new TrackingValidationException("Choose a valid report responsibility.")
        };
        MarkAssignmentChange(report, request.RowVersion);
        if (!divisionId.HasValue) return NoContent();

        var now = DateTime.UtcNow;
        var assignments = await Current(reportId, now).Where(x => x.ResponsibilityType == type && x.DivisionId == divisionId)
            .ToListAsync(ct);
        if (assignments.Count == 0) return NoContent();
        if (type == TrackingAccess.ComponentReceiverResponsibility &&
            assignments.SelectMany(x => new Guid?[] { x.PrimaryAssigneeId, x.AlternateAssigneeId })
                .Where(x => x.HasValue).Distinct().Count() > 2)
            throw new TrackingConflictException("This report has more than two receiving officers. Use the Receiving officers list to manage that team.");

        db.ResponsibilityAssignments.RemoveRange(assignments);
        report.UpdatedAtUtc = now;
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(ReportDefinition), EntityId = reportId, ActorUserId = access.UserId, Action = "ReportResponsibilityUnassigned",
            DataJson = JsonSerializer.Serialize(new
            {
                ResponsibilityType = type,
                DivisionId = divisionId,
                Removed = assignments.Select(x => new { x.Id, x.PrimaryAssigneeId, x.AlternateAssigneeId, x.EffectiveFromUtc })
            })
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void MarkAssignmentChange(ReportDefinition report, string? rowVersion)
    {
        byte[] version;
        try { version = Convert.FromBase64String(rowVersion ?? ""); }
        catch (FormatException) { throw new TrackingValidationException("Reload responsibilities and try again."); }
        if (version.Length != 8) throw new TrackingValidationException("Reload responsibilities and try again.");
        if (!version.SequenceEqual(report.RowVersion))
            throw new TrackingConflictException("This report changed since you loaded it. Reload responsibilities and try again.");
        db.Entry(report).Property(x => x.RowVersion).OriginalValue = version;
    }

    private IQueryable<ResponsibilityAssignment> Current(Guid reportId, DateTime now) => db.ResponsibilityAssignments.Where(x =>
        x.ReportDefinitionId == reportId && x.EffectiveFromUtc <= now && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now));

    private async Task ValidatePair(string name, Guid? divisionId, AssigneePair? pair, CancellationToken ct)
    {
        if (!divisionId.HasValue || !await db.Divisions.AnyAsync(x => x.Id == divisionId && x.IsActive, ct))
            throw new TrackingValidationException($"Configure an active division for {name} before assigning responsibilities.");
        if (pair is null || pair.PrimaryAssigneeId == Guid.Empty)
            throw new TrackingValidationException($"Choose a primary assignee for {name}.");
        if (pair.AlternateAssigneeId == pair.PrimaryAssigneeId)
            throw new TrackingValidationException($"Choose a different alternate assignee for {name}, or select None.");
        var ids = new Guid?[] { pair.PrimaryAssigneeId, pair.AlternateAssigneeId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (await db.Users.CountAsync(x => ids.Contains(x.Id) && x.IsActive && x.DivisionId == divisionId, ct) != ids.Length)
            throw new TrackingValidationException($"The {name} assignees must be active members of the displayed division.");
    }
}
