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
        if (report.ReceivingDivisionId.HasValue && request.ReceivingDivisionId != report.ReceivingDivisionId)
            throw new TrackingValidationException("Components must use the receiving division configured on the report.");
        var version = await db.DefinitionVersions.Where(x => x.ReportDefinitionId == reportId)
            .OrderByDescending(x => x.VersionNumber).FirstAsync(ct);
        request = request with { DeadlineOffsetDays = version.ComponentDeadlineOffsetDays };
        if (request.Contributors is not null)
        {
            var (primary, deputy) = await ComponentValidation.ValidateContributorsAsync(db, request.ProviderDivisionId, request.Contributors, ct);
            request = request with { PrimaryOfficerId = primary, DeputyOfficerId = deputy };
        }
        await ValidateAssignment(request, ct);
        var component = new ComponentDefinition
        {
            DefinitionVersionId = version.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsRequired = request.IsRequired,
            Sequence = request.Sequence,
            ProviderDivisionId = request.ProviderDivisionId,
            PrimaryOfficerId = request.PrimaryOfficerId,
            DeputyOfficerId = request.DeputyOfficerId,
            ContributorsJson = JsonSerializer.Serialize(request.Contributors ??
                ComponentValidation.Contributors(null, request.PrimaryOfficerId, request.DeputyOfficerId)),
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

    [HttpPut("{componentId:guid}/contributors")]
    public async Task<ActionResult<ComponentItem>> SetContributors(Guid reportId, Guid componentId, SetComponentContributorsRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        var versionId = await db.DefinitionVersions.Where(x => x.ReportDefinitionId == reportId)
            .OrderByDescending(x => x.VersionNumber).Select(x => x.Id).FirstAsync(ct);
        var component = await db.ComponentDefinitions.SingleOrDefaultAsync(x => x.Id == componentId && x.DefinitionVersionId == versionId, ct);
        if (component is null) return NotFound();
        var (primary, deputy) = await ComponentValidation.ValidateContributorsAsync(db, component.ProviderDivisionId, request.Contributors, ct);
        var previous = ComponentValidation.Contributors(component.ContributorsJson, component.PrimaryOfficerId, component.DeputyOfficerId);
        component.PrimaryOfficerId = primary;
        component.DeputyOfficerId = deputy;
        component.ContributorsJson = JsonSerializer.Serialize(request.Contributors);
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(ComponentDefinition), EntityId = component.Id, ActorUserId = access.UserId,
            Action = "ComponentContributorsChanged", DataJson = JsonSerializer.Serialize(new { reportId, previous, request.Contributors })
        });
        await db.SaveChangesAsync(ct);
        return Ok(ToItem(component));
    }

    private async Task ValidateAssignment(SaveComponentRequest request, CancellationToken ct)
    {
        await ComponentValidation.ValidateAsync(db, request.Name, request.Description, request.Sequence, request.ProviderDivisionId,
            request.ReceivingDivisionId, request.PrimaryOfficerId, request.DeputyOfficerId, request.DeadlineOffsetDays, ct);
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
            row.ReceiptConfirmationRequired, row.EvidenceRequired, offset,
            ComponentValidation.Contributors(row.ContributorsJson, row.PrimaryOfficerId, row.DeputyOfficerId));
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
            Label(x.SendingStatus), Label(x.EvidenceStatus), Label(x.ReceiptStatus), x.IsRequired && !Complete(x))).ToList();
        var required = components.Count(x => x.IsRequired);
        var completed = components.Count(x => x.IsRequired && Complete(x));
        return Ok(new SubmissionReadinessDto(cycleId, main.Id, main.ReportCycle.ReportDefinition.Name, main.ReportCycle.PeriodLabel,
            DateTime.SpecifyKind(main.EffectiveDueAtUtc, DateTimeKind.Utc), required, completed, required - completed,
            components.Count > 0 && completed == required, rows));
    }

    private static bool Complete(TrackingObligation x) =>
        x.SendingStatus == SendingStatus.Sent &&
        x.EvidenceStatus is EvidenceStatus.NotRequired or EvidenceStatus.Verified &&
        x.ReceiptStatus is ReceiptStatus.NotRequired or ReceiptStatus.ReceiptConfirmed;

    private static string Label<T>(T status) where T : Enum => string.Concat(status.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
