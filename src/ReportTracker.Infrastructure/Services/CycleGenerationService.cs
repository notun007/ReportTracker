using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

internal sealed class CycleGenerationService(ReportTrackerDbContext db) : ICycleGenerationService
{
    public async Task<GenerationResult> GenerateAsync(DateOnly throughLocalDate, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrackingRules.Dhaka));
        if (throughLocalDate < today || throughLocalDate > today.AddYears(2)) throw new TrackingValidationException("Generate cycles from today through at most two years ahead.");
        var reports = await db.ReportDefinitions.Where(x => x.IsActive).Include(x => x.Versions).Include(x => x.SubmissionRecipient)
            .Include(x => x.SubmissionDepartments).ThenInclude(x => x.Department).AsSplitQuery().ToListAsync(ct);
        var reportIds = reports.Select(x => x.Id).ToArray();
        var assignments = await db.ResponsibilityAssignments.Where(x => reportIds.Contains(x.ReportDefinitionId) && x.ResponsibilityType == "MainSender" &&
            x.EffectiveFromUtc <= DateTime.UtcNow && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc >= DateTime.UtcNow)).ToListAsync(ct);
        var activeUsers = await db.Users.Where(x => x.IsActive).ToListAsync(ct);
        var activeDivisions = await db.Divisions.Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct);
        var existing = await db.ReportCycles.Where(x => reportIds.Contains(x.ReportDefinitionId)).Select(x => new { x.ReportDefinitionId, x.PeriodKey }).ToListAsync(ct);
        var existingKeys = existing.Select(x => $"{x.ReportDefinitionId:N}:{x.PeriodKey}").ToHashSet();
        var warnings = new List<string>();
        var cyclesCreated = 0; var componentsCreated = 0;

        foreach (var report in reports)
        {
            var version = report.Versions.Where(x => x.EffectiveFrom <= throughLocalDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today))
                .OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (version is null || version.Frequency == Frequency.AdHoc) continue;
            var components = await db.ComponentDefinitions.Where(x => x.DefinitionVersionId == version.Id).OrderBy(x => x.Sequence).ToListAsync(ct);
            var mainAssignment = assignments.Where(x => x.ReportDefinitionId == report.Id).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefault();
            if (!activeDivisions.Contains(report.OwnerDivisionId) || !report.SubmittingDivisionId.HasValue || !activeDivisions.Contains(report.SubmittingDivisionId.Value) ||
                mainAssignment is null || mainAssignment.DivisionId != report.SubmittingDivisionId ||
                !activeUsers.Any(x => x.Id == mainAssignment.PrimaryOfficerId && x.DivisionId == report.SubmittingDivisionId) ||
                (mainAssignment.DeputyOfficerId.HasValue && !activeUsers.Any(x => x.Id == mainAssignment.DeputyOfficerId && x.DivisionId == report.SubmittingDivisionId)))
            { warnings.Add($"{report.Code}: configure an active coordinating division, final submitting division and final submitting officers in Report responsibilities."); continue; }
            var mainOfficerId = mainAssignment.PrimaryOfficerId;
            var destination = report.SubmissionDestination();
            if (components.Any(c => !activeDivisions.Contains(c.ProviderDivisionId) || !activeDivisions.Contains(report.ReceivingDivisionId ?? c.ReceivingDivisionId) ||
                !activeUsers.Any(x => x.Id == c.PrimaryOfficerId && x.DivisionId == c.ProviderDivisionId) ||
                (c.DeputyOfficerId.HasValue && !activeUsers.Any(x => x.Id == c.DeputyOfficerId && x.DivisionId == c.ProviderDivisionId))))
            { warnings.Add($"{report.Code}: one or more components need active preparing/receiving divisions and preparing officers."); continue; }
            var periods = Periods(version.Frequency, today, throughLocalDate).DistinctBy(x => x.Key).ToList();
            foreach (var period in periods)
            {
                var key = $"{report.Id:N}:{period.Key}";
                if (existingKeys.Contains(key)) continue;
                var schedule = await db.ScheduleRules.AsNoTracking().FirstOrDefaultAsync(x => x.DefinitionVersionId == version.Id, ct);
                var dueDate = schedule?.FixedDay is int fixedDay
                    ? new DateOnly(period.End.Year, period.End.Month, Math.Min(fixedDay, DateTime.DaysInMonth(period.End.Year, period.End.Month)))
                    : period.End.AddDays(schedule?.OffsetDays ?? 0);
                var localDue = DateTime.SpecifyKind(dueDate.ToDateTime(version.DueTime), DateTimeKind.Unspecified);
                var mainDueUtc = TimeZoneInfo.ConvertTimeToUtc(localDue, TrackingRules.Dhaka);
                var cycle = new ReportCycle { ReportDefinitionId = report.Id, DefinitionVersionId = version.Id, PeriodKey = period.Key,
                    PeriodLabel = period.Label, PeriodStart = period.Start, PeriodEnd = period.End };
                var main = new TrackingObligation { ReportCycleId = cycle.Id, Type = ObligationType.External, Name = TrackingRules.FinalSubmissionName,
                    AccountableDivisionId = report.SubmittingDivisionId.Value, PrimaryOfficerId = mainOfficerId, DeputyOfficerId = mainAssignment.DeputyOfficerId,
                    Recipient = destination, OriginalDueAtUtc = mainDueUtc, EffectiveDueAtUtc = mainDueUtc,
                    ReceiptStatus = version.ReceiptConfirmationRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                    EvidenceStatus = EvidenceStatus.Missing };
                cycle.Obligations.Add(main);
                foreach (var component in components)
                {
                    var leadDays = component.DeadlineRuleJson is null ? version.ComponentDeadlineOffsetDays : ParseOffset(component.DeadlineRuleJson, version.ComponentDeadlineOffsetDays);
                    var dueUtc = mainDueUtc.AddDays(-leadDays);
                    cycle.Obligations.Add(new TrackingObligation { ReportCycleId = cycle.Id, ParentObligationId = main.Id, Type = ObligationType.Internal,
                        Name = component.Name, IsRequired = component.IsRequired, AccountableDivisionId = component.ProviderDivisionId, PrimaryOfficerId = component.PrimaryOfficerId,
                        DeputyOfficerId = component.DeputyOfficerId, ReceivingDivisionId = report.ReceivingDivisionId ?? component.ReceivingDivisionId,
                        Recipient = (report.ReceivingDivisionId ?? component.ReceivingDivisionId).ToString(), OriginalDueAtUtc = dueUtc,
                        EffectiveDueAtUtc = dueUtc, ReceiptStatus = component.ReceiptConfirmationRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                        EvidenceStatus = component.EvidenceRequired ? EvidenceStatus.Missing : EvidenceStatus.NotRequired } );
                    componentsCreated++;
                }
                db.ReportCycles.Add(cycle);
                db.AuditEvents.Add(new AuditEvent { EntityType = nameof(ReportCycle), EntityId = cycle.Id, Action = "CycleAutomaticallyGenerated",
                    DataJson = JsonSerializer.Serialize(new { report.Id, period.Key, ComponentCount = components.Count }) });
                existingKeys.Add(key); cyclesCreated++;
            }
        }
        if (cyclesCreated > 0) await db.SaveChangesAsync(ct);
        return new(reports.Count, cyclesCreated, componentsCreated, warnings);
    }

    private static int ParseOffset(string json, int fallback)
    {
        try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty("offsetDays", out var value) ? value.GetInt32() : fallback; }
        catch (JsonException) { return fallback; }
    }

    private static IEnumerable<(string Key, string Label, DateOnly Start, DateOnly End)> Periods(Frequency frequency, DateOnly from, DateOnly through)
    {
        var cursor = from;
        while (cursor <= through)
        {
            var period = Period(frequency, cursor);
            yield return period;
            cursor = period.End.AddDays(1);
        }
    }

    private static (string Key, string Label, DateOnly Start, DateOnly End) Period(Frequency frequency, DateOnly date)
    {
        DateOnly start; DateOnly end; string key; string label;
        switch (frequency)
        {
            case Frequency.Daily: start = end = date; key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); label = date.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture); break;
            case Frequency.Weekly:
                start = date.AddDays(-(int)date.DayOfWeek); end = start.AddDays(6); key = $"{start:yyyy-MM-dd}"; label = $"Week of {start:dd MMM yyyy}"; break;
            case Frequency.Biweekly:
                var anchor = new DateOnly(2026, 1, 4); var block = (date.DayNumber - anchor.DayNumber) / 14; start = anchor.AddDays(block * 14); if (start > date) start = start.AddDays(-14); end = start.AddDays(13); key = $"BW-{start:yyyy-MM-dd}"; label = $"{start:dd MMM} - {end:dd MMM yyyy}"; break;
            case Frequency.Monthly:
                start = new(date.Year, date.Month, 1); end = start.AddMonths(1).AddDays(-1); key = $"{date:yyyy-MM}"; label = date.ToString("MMMM yyyy", CultureInfo.InvariantCulture); break;
            case Frequency.Quarterly:
                var quarter = (date.Month - 1) / 3 + 1; start = new(date.Year, (quarter - 1) * 3 + 1, 1); end = start.AddMonths(3).AddDays(-1); key = $"{date.Year}-Q{quarter}"; label = $"Q{quarter} {date.Year}"; break;
            case Frequency.HalfYearly:
                var half = date.Month <= 6 ? 1 : 2; start = new(date.Year, half == 1 ? 1 : 7, 1); end = start.AddMonths(6).AddDays(-1); key = $"{date.Year}-H{half}"; label = $"H{half} {date.Year}"; break;
            default:
                start = new(date.Year, 1, 1); end = new(date.Year, 12, 31); key = date.Year.ToString(CultureInfo.InvariantCulture); label = date.Year.ToString(CultureInfo.InvariantCulture); break;
        }
        return (key, label, start, end);
    }
}

internal sealed class OperationsAutomationWorker(IServiceScopeFactory scopeFactory, ILogger<OperationsAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var generator = scope.ServiceProvider.GetRequiredService<ICycleGenerationService>();
                await generator.GenerateAsync(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrackingRules.Dhaka)).AddDays(60), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Scheduled ReportTracker automation failed."); }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
