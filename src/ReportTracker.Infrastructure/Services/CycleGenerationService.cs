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
    public async Task<YearlyCyclePreview> PreviewYearAsync(int year, Guid? reportId, CancellationToken ct)
    {
        ValidateYear(year);
        var reports = await db.ReportDefinitions.AsNoTracking().Where(x => x.IsActive && (!reportId.HasValue || x.Id == reportId.Value))
            .Include(x => x.Versions).OrderBy(x => x.Code).ToListAsync(ct);
        var versionIds = reports.SelectMany(x => x.Versions).Select(x => x.Id).ToArray();
        var savedSchedules = await db.ScheduleRules.AsNoTracking().Where(x => versionIds.Contains(x.DefinitionVersionId))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var schedules = savedSchedules.GroupBy(x => x.DefinitionVersionId).ToDictionary(x => x.Key, x => x.First());
        var savedComponents = await db.ComponentDefinitions.AsNoTracking().Where(x => versionIds.Contains(x.DefinitionVersionId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        var components = savedComponents.GroupBy(x => x.DefinitionVersionId).ToDictionary(x => x.Key, x => x.ToArray());
        var presets = await db.SchedulePresets.AsNoTracking().OrderByDescending(x => x.Version).ToListAsync(ct);
        var reportIds = reports.Select(x => x.Id).ToArray();
        var existing = await db.ReportCycles.AsNoTracking().Where(x => reportIds.Contains(x.ReportDefinitionId))
            .Include(x => x.Obligations).ToListAsync(ct);
        var existingCycles = existing.GroupBy(x => $"{x.ReportDefinitionId:N}:{x.PeriodKey}")
            .ToDictionary(x => x.Key, x => x.First());
        var items = new List<YearlyCyclePlanItem>();

        foreach (var report in reports)
        {
            var version = report.Versions.Where(x => x.EffectiveFrom.Year <= year && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Year >= year))
                .OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (version is null)
            {
                items.Add(new(report.Id, report.Code, report.Name, "Unconfigured", $"{year}", $"{year}", new(year, 1, 1), new(year, 12, 31),
                    "Skipped", "No report definition version is effective for this year."));
                continue;
            }
            if (version.Frequency is Frequency.AdHoc or Frequency.Daily)
            {
                items.Add(new(report.Id, report.Code, report.Name, version.Frequency.ToString(), $"{year}", $"{year}", new(year, 1, 1), new(year, 12, 31),
                    "Skipped", version.Frequency == Frequency.AdHoc ? "Ad-hoc reports are not generated yearly." : "Daily bulk generation is not supported."));
                continue;
            }
            var ruleCode = RuleCode(version.Frequency);
            var preset = presets.FirstOrDefault(x => x.RuleCode == ruleCode);
            foreach (var period in PeriodsForYear(version.Frequency, year, preset))
            {
                var key = $"{report.Id:N}:{period.Key}";
                var status = existingCycles.TryGetValue(key, out var existingCycle) ? "Existing" : "New";
                var savedMain = existingCycle?.Obligations.FirstOrDefault(x => x.Type == ObligationType.External);
                var finalDeadline = savedMain is null
                    ? DeadlineDate(period.End, schedules.GetValueOrDefault(version.Id))
                    : DhakaDate(savedMain.OriginalDueAtUtc);
                var defaultComponentDeadline = finalDeadline.AddDays(-version.ComponentDeadlineOffsetDays);
                var componentDeadlines = existingCycle is null
                    ? components.GetValueOrDefault(version.Id, [])
                        .Select(component => new ComponentDeadlinePlanItem(component.Id, component.Name,
                            finalDeadline.AddDays(-ComponentLeadDays(component, version.ComponentDeadlineOffsetDays))))
                        .ToArray()
                    : existingCycle.Obligations.Where(x => x.Type == ObligationType.Internal).OrderBy(x => x.Name)
                        .Select(component => new ComponentDeadlinePlanItem(component.Id, component.Name, DhakaDate(component.OriginalDueAtUtc)))
                        .ToArray();
                items.Add(new(report.Id, report.Code, report.Name, version.Frequency.ToString(), period.Key, period.Label,
                    period.Start, period.End, status, null, finalDeadline, defaultComponentDeadline, componentDeadlines));
            }
        }
        return new(year, reports.Count, items.Count(x => x.Status != "Skipped"), items.Count(x => x.Status == "New"),
            items.Count(x => x.Status == "Existing"), items.Count(x => x.Status == "Skipped"), items);
    }

    public async Task<YearlyCycleGenerationResult> GenerateYearAsync(int year, Guid? reportId,
        IReadOnlyCollection<string>? periodKeys, CancellationToken ct)
    {
        var preview = await PreviewYearAsync(year, reportId, ct);
        var selectedKeys = periodKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var planned = preview.Items.Where(x => x.Status == "New" && (selectedKeys is null || selectedKeys.Contains(x.PeriodKey))).ToList();
        var reportIds = planned.Select(x => x.ReportId).Distinct().ToArray();
        var reports = await db.ReportDefinitions.Where(x => reportIds.Contains(x.Id)).Include(x => x.Versions)
            .Include(x => x.SubmissionRecipient).Include(x => x.SubmissionDepartments).ThenInclude(x => x.Department)
            .AsSplitQuery().ToDictionaryAsync(x => x.Id, ct);
        var assignments = await db.ResponsibilityAssignments.Where(x => reportIds.Contains(x.ReportDefinitionId) && x.ResponsibilityType == "MainSender" &&
            x.EffectiveFromUtc <= DateTime.UtcNow && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc >= DateTime.UtcNow))
            .OrderByDescending(x => x.EffectiveFromUtc).ToListAsync(ct);
        var activeUsers = await db.Users.Where(x => x.IsActive).ToListAsync(ct);
        var activeDivisions = await db.Divisions.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var warnings = preview.Items.Where(x => x.Status == "Skipped").Select(x => $"{x.ReportCode}: {x.Reason}").Distinct().ToList();
        var cyclesCreated = 0; var componentsCreated = 0; var skipped = preview.SkippedCycles;

        foreach (var item in planned)
        {
            var report = reports[item.ReportId];
            var version = report.Versions.Where(x => x.EffectiveFrom.Year <= year && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value.Year >= year))
                .OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            var assignment = assignments.FirstOrDefault(x => x.ReportDefinitionId == report.Id);
            if (version is null || !activeDivisions.ContainsKey(report.OwnerDivisionId) || !report.SubmittingDivisionId.HasValue ||
                !activeDivisions.ContainsKey(report.SubmittingDivisionId.Value) || assignment is null || assignment.DivisionId != report.SubmittingDivisionId ||
                !activeUsers.Any(x => x.Id == assignment.PrimaryAssigneeId && x.DivisionId == report.SubmittingDivisionId) ||
                (assignment.AlternateAssigneeId.HasValue && !activeUsers.Any(x => x.Id == assignment.AlternateAssigneeId && x.DivisionId == report.SubmittingDivisionId)))
            {
                warnings.Add($"{report.Code} / {item.PeriodKey}: check the report's responsible divisions and complete the final submitting assignee in Responsibilities before generating this cycle."); skipped++; continue;
            }
            var components = await db.ComponentDefinitions.Where(x => x.DefinitionVersionId == version.Id).OrderBy(x => x.Name).ToListAsync(ct);
            if (components.Any(c => !activeDivisions.ContainsKey(c.ProviderDivisionId) || !activeDivisions.ContainsKey(report.ReceivingDivisionId ?? c.ReceivingDivisionId) ||
                !activeUsers.Any(x => x.Id == c.PrimaryAssigneeId && x.DivisionId == c.ProviderDivisionId) ||
                (c.AlternateAssigneeId.HasValue && !activeUsers.Any(x => x.Id == c.AlternateAssigneeId && x.DivisionId == c.ProviderDivisionId))))
            {
                warnings.Add($"{report.Code} / {item.PeriodKey}: complete component responsibilities before generating this cycle."); skipped++; continue;
            }
            var dueDate = item.FinalDeadline ?? item.PeriodEnd;
            var localDue = DateTime.SpecifyKind(dueDate.ToDateTime(version.DueTime), DateTimeKind.Unspecified);
            var dueUtc = TimeZoneInfo.ConvertTimeToUtc(localDue, TrackingRules.Dhaka);
            var cycle = new ReportCycle { ReportDefinitionId = report.Id, DefinitionVersionId = version.Id, PeriodKey = item.PeriodKey,
                PeriodLabel = item.PeriodLabel, PeriodStart = item.PeriodStart, PeriodEnd = item.PeriodEnd };
            var main = new TrackingObligation { ReportCycleId = cycle.Id, Type = ObligationType.External, Name = TrackingRules.FinalSubmissionName,
                AccountableDivisionId = report.SubmittingDivisionId.Value, PrimaryAssigneeId = assignment.PrimaryAssigneeId, AlternateAssigneeId = assignment.AlternateAssigneeId,
                Recipient = report.SubmissionDestination(), OriginalDueAtUtc = dueUtc, EffectiveDueAtUtc = dueUtc,
                ReceiptStatus = version.ReceiptConfirmationRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                EvidenceStatus = EvidenceStatus.Missing };
            cycle.Obligations.Add(main);
            foreach (var component in components)
            {
                var leadDays = ComponentLeadDays(component, version.ComponentDeadlineOffsetDays);
                var componentDueUtc = dueUtc.AddDays(-leadDays);
                cycle.Obligations.Add(new TrackingObligation { ReportCycleId = cycle.Id, ParentObligationId = main.Id, Type = ObligationType.Internal,
                    Name = component.Name, AccountableDivisionId = component.ProviderDivisionId, PrimaryAssigneeId = component.PrimaryAssigneeId,
                    AlternateAssigneeId = component.AlternateAssigneeId, ReceivingDivisionId = report.ReceivingDivisionId ?? component.ReceivingDivisionId,
                    ContributorsJson = component.ContributorsJson ?? JsonSerializer.Serialize(ComponentValidation.Contributors(null, component.PrimaryAssigneeId, component.AlternateAssigneeId)),
                    Recipient = activeDivisions[report.ReceivingDivisionId ?? component.ReceivingDivisionId], OriginalDueAtUtc = componentDueUtc,
                    EffectiveDueAtUtc = componentDueUtc, ReceiptStatus = ReceiptStatus.NotRequired, EvidenceStatus = EvidenceStatus.NotRequired });
                componentsCreated++;
            }
            db.ReportCycles.Add(cycle);
            db.AuditEvents.Add(new AuditEvent { EntityType = nameof(ReportCycle), EntityId = cycle.Id, Action = "YearlyCyclesManuallyGenerated",
                DataJson = JsonSerializer.Serialize(new { year, report.Id, item.PeriodKey, item.PeriodStart, item.PeriodEnd, ComponentCount = components.Count }) });
            cyclesCreated++;
        }
        if (cyclesCreated > 0) await db.SaveChangesAsync(ct);
        return new(year, preview.ReportsEvaluated, cyclesCreated, preview.ExistingCycles, skipped, componentsCreated, warnings.Distinct().ToArray());
    }

    public async Task<GenerationResult> GenerateAsync(DateOnly throughLocalDate, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrackingRules.Dhaka));
        if (throughLocalDate < today || throughLocalDate > today.AddYears(2)) throw new TrackingValidationException("Generate cycles from today through at most two years ahead.");
        var reports = await db.ReportDefinitions.Where(x => x.IsActive).Include(x => x.Versions).Include(x => x.SubmissionRecipient)
            .Include(x => x.SubmissionDepartments).ThenInclude(x => x.Department).AsSplitQuery().ToListAsync(ct);
        var reportIds = reports.Select(x => x.Id).ToArray();
        var versionIds = reports.SelectMany(x => x.Versions).Select(x => x.Id).ToArray();
        var savedSchedules = await db.ScheduleRules.AsNoTracking().Where(x => versionIds.Contains(x.DefinitionVersionId))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var schedules = savedSchedules.GroupBy(x => x.DefinitionVersionId).ToDictionary(x => x.Key, x => x.First());
        var assignments = await db.ResponsibilityAssignments.Where(x => reportIds.Contains(x.ReportDefinitionId) && x.ResponsibilityType == "MainSender" &&
            x.EffectiveFromUtc <= DateTime.UtcNow && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc >= DateTime.UtcNow)).ToListAsync(ct);
        var activeUsers = await db.Users.Where(x => x.IsActive).ToListAsync(ct);
        var activeDivisions = await db.Divisions.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var existing = await db.ReportCycles.Where(x => reportIds.Contains(x.ReportDefinitionId)).Select(x => new { x.ReportDefinitionId, x.PeriodKey }).ToListAsync(ct);
        var existingKeys = existing.Select(x => $"{x.ReportDefinitionId:N}:{x.PeriodKey}").ToHashSet();
        var warnings = new List<string>();
        var cyclesCreated = 0; var componentsCreated = 0;

        foreach (var report in reports)
        {
            var version = report.Versions.Where(x => x.EffectiveFrom <= throughLocalDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today))
                .OrderByDescending(x => x.VersionNumber).FirstOrDefault();
            if (version is null || version.Frequency == Frequency.AdHoc) continue;
            var components = await db.ComponentDefinitions.Where(x => x.DefinitionVersionId == version.Id).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
            var mainAssignment = assignments.Where(x => x.ReportDefinitionId == report.Id).OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefault();
            if (!activeDivisions.ContainsKey(report.OwnerDivisionId) || !report.SubmittingDivisionId.HasValue || !activeDivisions.ContainsKey(report.SubmittingDivisionId.Value) ||
                mainAssignment is null || mainAssignment.DivisionId != report.SubmittingDivisionId ||
                !activeUsers.Any(x => x.Id == mainAssignment.PrimaryAssigneeId && x.DivisionId == report.SubmittingDivisionId) ||
                (mainAssignment.AlternateAssigneeId.HasValue && !activeUsers.Any(x => x.Id == mainAssignment.AlternateAssigneeId && x.DivisionId == report.SubmittingDivisionId)))
            { warnings.Add($"{report.Code}: check the coordinating and final submitting divisions in the report definition, then assign an active final submitting assignee in Responsibilities."); continue; }
            var mainOfficerId = mainAssignment.PrimaryAssigneeId;
            var destination = report.SubmissionDestination();
            if (components.Any(c => !activeDivisions.ContainsKey(c.ProviderDivisionId) || !activeDivisions.ContainsKey(report.ReceivingDivisionId ?? c.ReceivingDivisionId) ||
                !activeUsers.Any(x => x.Id == c.PrimaryAssigneeId && x.DivisionId == c.ProviderDivisionId) ||
                (c.AlternateAssigneeId.HasValue && !activeUsers.Any(x => x.Id == c.AlternateAssigneeId && x.DivisionId == c.ProviderDivisionId))))
            { warnings.Add($"{report.Code}: one or more components need active preparing/receiving divisions and preparing officers."); continue; }
            var periods = Periods(version.Frequency, today, throughLocalDate).DistinctBy(x => x.Key).ToList();
            foreach (var period in periods)
            {
                var key = $"{report.Id:N}:{period.Key}";
                if (existingKeys.Contains(key)) continue;
                var dueDate = DeadlineDate(period.End, schedules.GetValueOrDefault(version.Id));
                var localDue = DateTime.SpecifyKind(dueDate.ToDateTime(version.DueTime), DateTimeKind.Unspecified);
                var mainDueUtc = TimeZoneInfo.ConvertTimeToUtc(localDue, TrackingRules.Dhaka);
                var cycle = new ReportCycle { ReportDefinitionId = report.Id, DefinitionVersionId = version.Id, PeriodKey = period.Key,
                    PeriodLabel = period.Label, PeriodStart = period.Start, PeriodEnd = period.End };
                var main = new TrackingObligation { ReportCycleId = cycle.Id, Type = ObligationType.External, Name = TrackingRules.FinalSubmissionName,
                    AccountableDivisionId = report.SubmittingDivisionId.Value, PrimaryAssigneeId = mainOfficerId, AlternateAssigneeId = mainAssignment.AlternateAssigneeId,
                    Recipient = destination, OriginalDueAtUtc = mainDueUtc, EffectiveDueAtUtc = mainDueUtc,
                    ReceiptStatus = version.ReceiptConfirmationRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                    EvidenceStatus = EvidenceStatus.Missing };
                cycle.Obligations.Add(main);
                foreach (var component in components)
                {
                    var leadDays = ComponentLeadDays(component, version.ComponentDeadlineOffsetDays);
                    var dueUtc = mainDueUtc.AddDays(-leadDays);
                    cycle.Obligations.Add(new TrackingObligation { ReportCycleId = cycle.Id, ParentObligationId = main.Id, Type = ObligationType.Internal,
                        Name = component.Name, AccountableDivisionId = component.ProviderDivisionId, PrimaryAssigneeId = component.PrimaryAssigneeId,
                        AlternateAssigneeId = component.AlternateAssigneeId, ReceivingDivisionId = report.ReceivingDivisionId ?? component.ReceivingDivisionId,
                        ContributorsJson = component.ContributorsJson ?? JsonSerializer.Serialize(ComponentValidation.Contributors(null, component.PrimaryAssigneeId, component.AlternateAssigneeId)),
                        Recipient = activeDivisions[report.ReceivingDivisionId ?? component.ReceivingDivisionId], OriginalDueAtUtc = dueUtc,
                        EffectiveDueAtUtc = dueUtc, ReceiptStatus = ReceiptStatus.NotRequired,
                        EvidenceStatus = EvidenceStatus.NotRequired } );
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

    private static int ComponentLeadDays(ComponentDefinition component, int fallback) =>
        component.DeadlineRuleJson is null ? fallback : ParseOffset(component.DeadlineRuleJson, fallback);

    private static DateOnly DeadlineDate(DateOnly periodEnd, ScheduleRule? schedule) =>
        schedule?.FixedDay is int fixedDay
            ? new DateOnly(periodEnd.Year, periodEnd.Month, Math.Min(fixedDay, DateTime.DaysInMonth(periodEnd.Year, periodEnd.Month)))
            : periodEnd.AddDays(schedule?.OffsetDays ?? 0);

    private static DateOnly DhakaDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc), TrackingRules.Dhaka));

    private static void ValidateYear(int year)
    {
        if (year is < 2000 or > 9998) throw new TrackingValidationException("Choose a year between 2000 and 9998.");
    }

    private static string RuleCode(Frequency frequency) => frequency switch
    {
        Frequency.Weekly => "weekly", Frequency.Biweekly => "twice-monthly", Frequency.Monthly => "monthly",
        Frequency.Quarterly => "quarterly", Frequency.HalfYearly => "half-yearly", Frequency.Yearly => "yearly",
        _ => throw new TrackingValidationException("This reporting frequency cannot use yearly bulk generation.")
    };

    private static IReadOnlyList<(string Key, string Label, DateOnly Start, DateOnly End)> PeriodsForYear(Frequency frequency, int year, SchedulePreset? preset)
    {
        var configured = ReadConfiguredPeriods(preset);
        var periods = new List<(string, string, DateOnly, DateOnly)>();
        if (frequency is Frequency.Weekly or Frequency.Biweekly)
        {
            var count = frequency == Frequency.Weekly ? 4 : 2;
            var defaults = frequency == Frequency.Weekly ? new (int, int?)[] { (1, 7), (8, 15), (16, 21), (22, null) } : [(1, 15), (16, null)];
            for (var month = 1; month <= 12; month++) for (var index = 0; index < count; index++)
            {
                var source = configured?[index]; var start = ResolveDate(year, month, source?.StartDay ?? defaults[index].Item1);
                var end = ResolveDate(year, month, source?.EndDay ?? defaults[index].Item2);
                var suffix = frequency == Frequency.Weekly ? $"W{index + 1}" : $"P{index + 1}";
                periods.Add(($"{year}-{month:00}-{suffix}", $"{start:dd MMM} - {end:dd MMM yyyy}", start, end));
            }
            return periods;
        }
        var monthsPerPeriod = frequency switch { Frequency.Monthly => 1, Frequency.Quarterly => 3, Frequency.HalfYearly => 6, _ => 12 };
        var countPeriods = 12 / monthsPerPeriod;
        for (var index = 0; index < countPeriods; index++)
        {
            var firstMonth = index * monthsPerPeriod + 1; var lastMonth = firstMonth + monthsPerPeriod - 1; var source = configured?[index];
            var startMonth = source?.StartMonth ?? firstMonth; var endMonth = source?.EndMonth ?? lastMonth;
            var start = ResolveDate(year, startMonth, source?.StartDay ?? 1); var end = ResolveDate(year, endMonth, source?.EndDay);
            var key = frequency switch { Frequency.Monthly => $"{year}-{firstMonth:00}", Frequency.Quarterly => $"{year}-Q{index + 1}",
                Frequency.HalfYearly => $"{year}-H{index + 1}", _ => $"{year}" };
            var label = frequency switch { Frequency.Monthly => start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                Frequency.Quarterly => $"Q{index + 1} {year}", Frequency.HalfYearly => $"H{index + 1} {year}", _ => year.ToString(CultureInfo.InvariantCulture) };
            periods.Add((key, label, start, end));
        }
        return periods;
    }

    private static DateOnly ResolveDate(int year, int month, int? day) =>
        new(year, month, Math.Min(day ?? DateTime.DaysInMonth(year, month), DateTime.DaysInMonth(year, month)));

    private static WeeklyPresetPeriod[]? ReadConfiguredPeriods(SchedulePreset? preset)
    {
        if (preset?.WeeklyStartDays is null) return null;
        return preset.WeeklyStartDays.Split(',').Zip(preset.PeriodEndDays.Split(','), (start, end) =>
        {
            var (startDay, startMonth) = DecodeDay(start); var (endDay, endMonth) = DecodeDay(end);
            return new WeeklyPresetPeriod(startDay!.Value, endDay, startMonth, endMonth);
        }).ToArray();
    }

    private static (int? Day, int? Month) DecodeDay(string value)
    {
        var parts = value.Split('-');
        return (parts[^1] == "end" ? null : int.Parse(parts[^1], CultureInfo.InvariantCulture),
            parts.Length == 2 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : null);
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
