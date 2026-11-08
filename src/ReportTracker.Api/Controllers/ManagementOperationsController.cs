using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Application.Abstractions;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/extensions")]
public sealed class ExtensionsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExtensionItem>>> Get(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var authorized = access.AuthorizedObligations().Select(x => x.Id);
        var rows = await (from extension in db.ExtensionRequests.AsNoTracking()
            join obligation in db.TrackingObligations on extension.TrackingObligationId equals obligation.Id
            join cycle in db.ReportCycles on obligation.ReportCycleId equals cycle.Id
            join report in db.ReportDefinitions on cycle.ReportDefinitionId equals report.Id
            join user in db.Users on extension.RequestedByUserId equals user.Id
            where authorized.Contains(obligation.Id)
            orderby extension.CreatedAtUtc descending
            select new ExtensionItem(extension.Id, obligation.Id, report.Name, obligation.Name, cycle.PeriodLabel,
                extension.OriginalDueAtUtc, extension.RequestedDueAtUtc, extension.Reason, extension.PermissionReference,
                user.DisplayName, extension.Status.ToString(), extension.CreatedAtUtc, extension.DecidedAtUtc, extension.DecisionReason)).ToListAsync(ct);
        return Ok(rows.Select(x => x with
        {
            OriginalDueAtUtc = DateTime.SpecifyKind(x.OriginalDueAtUtc, DateTimeKind.Utc),
            RequestedDueAtUtc = DateTime.SpecifyKind(x.RequestedDueAtUtc, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
            DecidedAtUtc = x.DecidedAtUtc.HasValue ? DateTime.SpecifyKind(x.DecidedAtUtc.Value, DateTimeKind.Utc) : null
        }));
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, DecideExtensionRequest request, CancellationToken ct)
    {
        var extension = await db.ExtensionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (extension is null) return NotFound();
        var obligation = await db.TrackingObligations.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .FirstOrDefaultAsync(x => x.Id == extension.TrackingObligationId, ct);
        if (obligation is null) return NotFound();
        await access.RequireAsync("reports.manage", obligation.ReportCycle.ReportDefinition.OwnerDivisionId, obligation.ReportCycle.ReportDefinitionId, ct);
        if (extension.Status != ExtensionStatus.Pending) throw new TrackingConflictException("This extension request has already been decided.");
        if (extension.RequestedByUserId == access.UserId) throw new TrackingConflictException("The requester cannot approve or reject their own extension request.");
        if (!Enum.TryParse<ExtensionStatus>(request.Decision, true, out var decision) || decision is not (ExtensionStatus.Approved or ExtensionStatus.Rejected))
            throw new TrackingValidationException("Choose Approved or Rejected.");
        extension.Status = decision; extension.ApproverUserId = access.UserId; extension.DecidedAtUtc = DateTime.UtcNow; extension.DecisionReason = request.Reason.Trim();
        if (decision == ExtensionStatus.Approved) obligation.EffectiveDueAtUtc = extension.RequestedDueAtUtc;
        db.ExtensionDecisions.Add(new ExtensionDecision { ExtensionRequestId = id, DecidedByUserId = access.UserId,
            Decision = decision, Reason = request.Reason.Trim(), DecidedAtUtc = extension.DecidedAtUtc.Value });
        db.AuditEvents.Add(new AuditEvent { EntityType = nameof(ExtensionRequest), EntityId = id, ActorUserId = access.UserId,
            Action = "Extension" + decision, DataJson = JsonSerializer.Serialize(new { obligation.Id, extension.RequestedDueAtUtc, request.Reason }) });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

[ApiController, Authorize, Route("api/reminders")]
public sealed class RemindersController(ReportTrackerDbContext db, TrackingAccess access, INotificationAutomationService automation) : ControllerBase
{
    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<ReminderPolicyItem>>> Policies(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.Grants("reports.manage").AnyAsync(ct) && !await access.HasAsync("administration.manage", null, null, ct)) return Forbid();
        var rows = await db.ReminderPolicies.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var reports = await db.ReportDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Ok(rows.Select(x => new ReminderPolicyItem(x.Id, x.Name, x.ReportDefinitionId,
            x.ReportDefinitionId.HasValue ? reports.GetValueOrDefault(x.ReportDefinitionId.Value, "Unknown report") : "All reports",
            x.ObligationCategory, x.LeadDaysCsv, x.DispatchTime, x.ChannelsCsv, x.RecipientsCsv, x.DailyOverdueReminders,
            x.WorkingDaysCsv, x.QuietHoursStart, x.QuietHoursEnd, x.EscalationAfterDays, x.IsActive)));
    }

    [HttpPost("policies")]
    public async Task<ActionResult> CreatePolicy(SaveReminderPolicyRequest request, CancellationToken ct)
    {
        if (request.ReportDefinitionId.HasValue)
        {
            var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == request.ReportDefinitionId && x.IsActive, ct);
            if (report is null) throw new TrackingValidationException("Choose an active report.");
            await access.RequireAsync("reports.manage", report.OwnerDivisionId, report.Id, ct);
        }
        else await access.RequireAsync("administration.manage", null, null, ct);
        var leadDays = request.LeadDaysCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (leadDays.Length == 0 || leadDays.Any(x => !int.TryParse(x, out var day) || day is < 0 or > 365))
            throw new TrackingValidationException("Lead days must be comma-separated numbers between 0 and 365.");
        var channels = request.ChannelsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (channels.Length == 0 || channels.Any(x => x is not ("Email" or "SMS"))) throw new TrackingValidationException("Choose Email and/or SMS.");
        var policy = new ReminderPolicy { Name = request.Name.Trim(), ReportDefinitionId = request.ReportDefinitionId,
            ObligationCategory = request.ObligationCategory.Trim(), LeadDaysCsv = string.Join(',', leadDays.Distinct()), DispatchTime = request.DispatchTime,
            ChannelsCsv = string.Join(',', channels.Distinct()), RecipientsCsv = request.RecipientsCsv?.Trim() ?? "Primary,Deputy",
            DailyOverdueReminders = request.DailyOverdueReminders, WorkingDaysCsv = request.WorkingDaysCsv?.Trim() ?? "Sun,Mon,Tue,Wed,Thu",
            QuietHoursStart = request.QuietHoursStart, QuietHoursEnd = request.QuietHoursEnd,
            EscalationAfterDays = request.EscalationAfterDays, IsActive = request.IsActive };
        db.ReminderPolicies.Add(policy);
        db.AuditEvents.Add(new AuditEvent { EntityType = nameof(ReminderPolicy), EntityId = policy.Id, ActorUserId = access.UserId,
            Action = "ReminderPolicyCreated", DataJson = JsonSerializer.Serialize(new { policy.Name, policy.ReportDefinitionId, policy.ChannelsCsv }) });
        await db.SaveChangesAsync(ct);
        return Created($"/api/reminders/policies/{policy.Id}", new { policy.Id });
    }

    [HttpPost("plan")]
    public async Task<ActionResult> Plan(CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var created = await automation.PlanAsync(DateTime.UtcNow, ct);
        return Ok(new { created });
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult> Dispatch(CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var attempted = await automation.DispatchAsync(DateTime.UtcNow, ct);
        return Ok(new { attempted });
    }

    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyList<NotificationItem>>> Deliveries([FromQuery] int take = 100, CancellationToken ct = default)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.Grants("reports.manage").AnyAsync(ct) && !await access.HasAsync("administration.manage", null, null, ct)) return Forbid();
        take = Math.Clamp(take, 1, 500);
        var rows = await (from job in db.NotificationJobs.AsNoTracking()
            join obligation in db.TrackingObligations on job.TrackingObligationId equals obligation.Id
            join cycle in db.ReportCycles on obligation.ReportCycleId equals cycle.Id
            join report in db.ReportDefinitions on cycle.ReportDefinitionId equals report.Id
            join delivery in db.NotificationDeliveries on job.Id equals delivery.NotificationJobId into deliveries
            from delivery in deliveries.OrderByDescending(x => x.AttemptedAtUtc).Take(1).DefaultIfEmpty()
            orderby job.DueAtUtc descending
            select new NotificationItem(job.Id, obligation.Id, report.Name, obligation.Name, job.Recipient, job.Channel,
                job.DueAtUtc, job.Status, job.AttemptCount, delivery == null ? null : delivery.AttemptedAtUtc,
                delivery == null ? null : delivery.Succeeded, delivery == null ? null : delivery.ProviderReference,
                delivery == null ? null : delivery.FailureReason)).Take(take).ToListAsync(ct);
        return Ok(rows.Select(x => x with
        {
            DueAtUtc = DateTime.SpecifyKind(x.DueAtUtc, DateTimeKind.Utc),
            AttemptedAtUtc = x.AttemptedAtUtc.HasValue ? DateTime.SpecifyKind(x.AttemptedAtUtc.Value, DateTimeKind.Utc) : null
        }));
    }

    [HttpPost("jobs/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var job = await db.NotificationJobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return NotFound();
        job.Status = "Pending"; job.DueAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

[ApiController, Authorize, Route("api/operations")]
public sealed class OperationsInsightsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<CalendarItem>>> Calendar(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || toUtc <= fromUtc || toUtc - fromUtc > TimeSpan.FromDays(370))
            throw new TrackingValidationException("Choose a valid UTC calendar range of at most 370 days.");
        var now = DateTime.UtcNow;
        var rows = await access.AuthorizedObligations().AsNoTracking().Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Where(x => x.EffectiveDueAtUtc >= fromUtc && x.EffectiveDueAtUtc < toUtc && x.ExceptionStatus == ExceptionStatus.None)
            .OrderBy(x => x.EffectiveDueAtUtc).ToListAsync(ct);
        var divisions = await db.Divisions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var extensionIds = await db.ExtensionRequests.AsNoTracking().Where(x => x.Status == ExtensionStatus.Approved && rows.Select(r => r.Id).Contains(x.TrackingObligationId))
            .Select(x => x.TrackingObligationId).ToHashSetAsync(ct);
        return Ok(rows.Select(x => new CalendarItem(x.Id, x.ReportCycleId, x.ReportCycle.ReportDefinition.Name, x.Name,
            x.ReportCycle.PeriodLabel, x.Type == ObligationType.External ? "Main report" : "Component",
            divisions.GetValueOrDefault(x.AccountableDivisionId, "Unknown division"), DateTime.SpecifyKind(x.EffectiveDueAtUtc, DateTimeKind.Utc),
            TrackingRules.Timeliness(x.EffectiveDueAtUtc, x.SendingStatus == SendingStatus.Sent ? x.UpdatedAtUtc : null, now),
            x.SendingStatus.ToString(), extensionIds.Contains(x.Id))));
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<ManagementAnalyticsDto>> Analytics(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var now = DateTime.UtcNow;
        var rows = await access.AuthorizedObligations().AsNoTracking().Include(x => x.SendingEvents).ToListAsync(ct);
        var divisions = await db.Divisions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var submitted = rows.Count(x => x.SendingStatus == SendingStatus.Sent);
        var onTime = rows.Count(x => x.SendingStatus == SendingStatus.Sent && x.SendingEvents.Any(e => !e.IsReversed && !e.IsPartial && e.ActualSentAtUtc <= x.EffectiveDueAtUtc));
        var overdue = rows.Count(x => x.SendingStatus != SendingStatus.Sent && x.EffectiveDueAtUtc < now);
        var performance = rows.GroupBy(x => x.AccountableDivisionId).Select(group => new DivisionPerformanceItem(
            divisions.GetValueOrDefault(group.Key, "Unknown division"), group.Count(),
            group.Count(x => x.SendingStatus == SendingStatus.Sent && x.SendingEvents.Any(e => !e.IsReversed && !e.IsPartial && e.ActualSentAtUtc <= x.EffectiveDueAtUtc)),
            group.Count(x => x.SendingStatus == SendingStatus.Sent && x.SendingEvents.Any(e => !e.IsReversed && !e.IsPartial && e.ActualSentAtUtc > x.EffectiveDueAtUtc)),
            group.Count(x => x.SendingStatus != SendingStatus.Sent && x.EffectiveDueAtUtc < now))).OrderBy(x => x.Division).ToList();
        var authorizedIds = rows.Select(x => x.Id).ToArray();
        var pendingExtensions = await db.ExtensionRequests.CountAsync(x => authorizedIds.Contains(x.TrackingObligationId) && x.Status == ExtensionStatus.Pending, ct);
        var failedNotifications = await db.NotificationJobs.CountAsync(x => authorizedIds.Contains(x.TrackingObligationId) && x.Status == "Failed", ct);
        return Ok(new ManagementAnalyticsDto(rows.Count, submitted, overdue, rows.Count(x => x.SendingStatus != SendingStatus.Sent && x.EffectiveDueAtUtc < now.AddDays(7)),
            pendingExtensions, failedNotifications, submitted == 0 ? 0 : Math.Round((decimal)onTime / submitted * 100, 1), performance));
    }
}
