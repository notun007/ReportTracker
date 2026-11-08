using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/report-definitions/{reportId:guid}/components")]
public sealed class ComponentsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComponentItem>>> Get(Guid reportId, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        var versionId = await db.DefinitionVersions.Where(x => x.ReportDefinitionId == reportId)
            .OrderByDescending(x => x.VersionNumber).Select(x => x.Id).FirstAsync(ct);
        var rows = await db.ComponentDefinitions.AsNoTracking().Where(x => x.DefinitionVersionId == versionId)
            .OrderBy(x => x.Sequence).ThenBy(x => x.Name).ToListAsync(ct);
        return Ok(rows.Select(ToItem));
    }

    [HttpPost]
    public async Task<ActionResult<ComponentItem>> Create(Guid reportId, SaveComponentRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        await ValidateAssignment(request, ct);
        var versionId = await db.DefinitionVersions.Where(x => x.ReportDefinitionId == reportId)
            .OrderByDescending(x => x.VersionNumber).Select(x => x.Id).FirstAsync(ct);
        var component = new ComponentDefinition
        {
            DefinitionVersionId = versionId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsRequired = request.IsRequired,
            Sequence = request.Sequence,
            ProviderDivisionId = request.ProviderDivisionId,
            PrimaryOfficerId = request.PrimaryOfficerId,
            DeputyOfficerId = request.DeputyOfficerId,
            ReceivingDivisionId = request.ReceivingDivisionId,
            ReceiptConfirmationRequired = request.ReceiptRequired,
            EvidenceRequired = request.EvidenceRequired,
            DeadlineRuleJson = JsonSerializer.Serialize(new { offsetDays = request.DeadlineOffsetDays })
        };
        db.ComponentDefinitions.Add(component);
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(ComponentDefinition), EntityId = component.Id, ActorUserId = access.UserId,
            Action = "ComponentCreated", DataJson = JsonSerializer.Serialize(new { reportId, component.Name, component.ProviderDivisionId, component.IsRequired })
        });
        await db.SaveChangesAsync(ct);
        return Created($"/api/report-definitions/{reportId}/components/{component.Id}", ToItem(component));
    }

    private async Task ValidateAssignment(SaveComponentRequest request, CancellationToken ct)
    {
        var divisions = await db.Divisions.Where(x => x.IsActive && (x.Id == request.ProviderDivisionId || x.Id == request.ReceivingDivisionId))
            .Select(x => x.Id).ToListAsync(ct);
        if (!divisions.Contains(request.ProviderDivisionId) || !divisions.Contains(request.ReceivingDivisionId))
            throw new TrackingValidationException("Choose active preparing and receiving divisions.");
        if (request.PrimaryOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == request.PrimaryOfficerId && x.DivisionId == request.ProviderDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("The primary officer must be an active member of the preparing division.");
        if (request.DeputyOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == request.DeputyOfficerId && x.DivisionId == request.ProviderDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("The deputy officer must be an active member of the preparing division.");
    }

    private static ComponentItem ToItem(ComponentDefinition row)
    {
        var offset = 0;
        if (!string.IsNullOrWhiteSpace(row.DeadlineRuleJson))
        {
            using var json = JsonDocument.Parse(row.DeadlineRuleJson);
            offset = json.RootElement.TryGetProperty("offsetDays", out var value) ? value.GetInt32() : 0;
        }
        return new(row.Id, row.DefinitionVersionId, row.Name, row.Description, row.IsRequired, row.Sequence,
            row.ProviderDivisionId, row.PrimaryOfficerId, row.DeputyOfficerId, row.ReceivingDivisionId,
            row.ReceiptConfirmationRequired, row.EvidenceRequired, offset);
    }
}

[ApiController, Authorize, Route("api/report-cycles")]
public sealed class ReadinessController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet("{cycleId:guid}/readiness")]
    public async Task<ActionResult<SubmissionReadinessDto>> Get(Guid cycleId, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var main = await access.AuthorizedObligations().AsNoTracking()
            .Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .FirstOrDefaultAsync(x => x.ReportCycleId == cycleId && x.Type == ObligationType.External, ct);
        if (main is null) return NotFound();

        var components = await db.TrackingObligations.AsNoTracking()
            .Where(x => x.ParentObligationId == main.Id && x.ExceptionStatus == ExceptionStatus.None)
            .OrderBy(x => x.EffectiveDueAtUtc).ThenBy(x => x.Name).ToListAsync(ct);
        var divisions = await db.Divisions.AsNoTracking().Where(x => components.Select(c => c.AccountableDivisionId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var users = await db.Users.AsNoTracking().Where(x => components.Select(c => c.PrimaryOfficerId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var now = DateTime.UtcNow;
        var rows = components.Select(x => new ReadinessComponentDto(
            x.Id, x.Name, divisions.GetValueOrDefault(x.AccountableDivisionId, "Unknown division"),
            users.GetValueOrDefault(x.PrimaryOfficerId ?? Guid.Empty, "Unassigned"), DateTime.SpecifyKind(x.EffectiveDueAtUtc, DateTimeKind.Utc),
            TrackingRules.Timeliness(x.EffectiveDueAtUtc, x.SendingStatus == SendingStatus.Sent ? x.UpdatedAtUtc : null, now),
            Label(x.SendingStatus), Label(x.EvidenceStatus), Label(x.ReceiptStatus), !Complete(x))).ToList();
        var completed = rows.Count(x => !x.IsBlocking);
        return Ok(new SubmissionReadinessDto(cycleId, main.Id, main.ReportCycle.ReportDefinition.Name, main.ReportCycle.PeriodLabel,
            DateTime.SpecifyKind(main.EffectiveDueAtUtc, DateTimeKind.Utc), rows.Count, completed, rows.Count - completed,
            rows.Count > 0 && completed == rows.Count, rows));
    }

    private static bool Complete(TrackingObligation x) =>
        x.SendingStatus == SendingStatus.Sent &&
        x.EvidenceStatus is EvidenceStatus.NotRequired or EvidenceStatus.Verified &&
        x.ReceiptStatus is ReceiptStatus.NotRequired or ReceiptStatus.ReceiptConfirmed;

    private static string Label<T>(T status) where T : Enum => string.Concat(status.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
