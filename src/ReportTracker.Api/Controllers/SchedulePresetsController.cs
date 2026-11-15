using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/setup/schedule-presets")]
public sealed class SchedulePresetsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    private sealed record ScheduleRuleDetails(string Code, string ReportingPeriod, string FinalDeadline,
        int MonthsPerPeriod, string PeriodEndDays, int DeadlineMonthOffset, int? FixedDeadlineDay);

    private static readonly ScheduleRuleDetails[] Rules =
    [
        new("weekly", "1–7, 8–15, 16–21, 22–month end", "7th, 15th, 21st, and month end", 1, "7,15,21,end", 0, null),
        new("twice-monthly", "1–15 and 16–month end", "15th and month end", 1, "15,end", 0, null),
        new("monthly", "Full calendar month", "10th of the following month", 1, "end", 1, 10),
        new("quarterly", "Jan–Mar, Apr–Jun, Jul–Sep, Oct–Dec", "31 Mar, 30 Jun, 30 Sep, 31 Dec", 3, "end", 0, null),
        new("half-yearly", "Jan–Jun and Jul–Dec", "30 Jun and 31 Dec", 6, "end", 0, null),
        new("yearly", "Full calendar year", "31 Dec", 12, "end", 0, null)
    ];

    [HttpGet]
    public async Task<ActionResult<SchedulePresetDirectory>> List(CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var saved = await db.SchedulePresets.AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ThenByDescending(x => x.Version).ToListAsync(ct);
        var frequencies = await db.ReportingFrequencies.AsNoTracking()
            .OrderBy(frequency => frequency.DisplayOrder).ThenBy(frequency => frequency.Code).ToListAsync(ct);
        var rules = frequencies.Join(Rules, frequency => frequency.Code, rule => rule.Code, (frequency, rule) =>
            new SchedulePresetRule(frequency.Code, frequency.Name, frequency.DisplayOrder, rule.ReportingPeriod,
                rule.FinalDeadline, rule.MonthsPerPeriod, rule.PeriodEndDays, rule.DeadlineMonthOffset, rule.FixedDeadlineDay))
            .ToArray();
        return Ok(new SchedulePresetDirectory(rules, saved.Select(ToItem).ToArray()));
    }

    [HttpGet("preview")]
    public async Task<ActionResult<IReadOnlyList<SchedulePresetPeriod>>> Preview(string ruleCode, DateOnly month, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var rule = FindRule(ruleCode);
        if (month.Year is < 2000 or > 9998) throw new TrackingValidationException("Choose a preview month between 2000 and 9998.");
        var firstMonth = ((month.Month - 1) / rule.MonthsPerPeriod) * rule.MonthsPerPeriod + 1;
        var start = new DateOnly(month.Year, firstMonth, 1);
        var lastMonth = start.AddMonths(rule.MonthsPerPeriod - 1);
        var endOfMonth = lastMonth.AddMonths(1).AddDays(-1);
        if (EditablePeriodCount(ruleCode) > 0)
        {
            var saved = await db.SchedulePresets.AsNoTracking().Where(x => x.RuleCode == ruleCode)
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (ReadConfiguredPeriods(saved) is { } configuredPeriods)
            {
                if (rule.Code == "monthly")
                    configuredPeriods = [configuredPeriods[month.Month - 1]];
                else if (rule.MonthsPerPeriod > 1)
                    configuredPeriods = [configuredPeriods[(firstMonth - 1) / rule.MonthsPerPeriod]];
                return Ok(configuredPeriods.Select(period =>
                {
                    var periodStart = ResolveDate(month.Year, period.StartMonth ?? start.Month, period.StartDay);
                    var end = ResolveDate(month.Year, period.EndMonth ?? lastMonth.Month, period.EndDay);
                    return new SchedulePresetPeriod(periodStart, end, FinalDeadline(rule, end));
                }).ToArray());
            }
        }
        var periods = new List<SchedulePresetPeriod>();
        foreach (var endDay in rule.PeriodEndDays.Split(','))
        {
            var end = endDay == "end" ? endOfMonth : new DateOnly(lastMonth.Year, lastMonth.Month, int.Parse(endDay, CultureInfo.InvariantCulture));
            periods.Add(new(start, end, FinalDeadline(rule, end)));
            start = end.AddDays(1);
        }
        return Ok(periods);
    }

    [HttpPost]
    public async Task<ActionResult<SchedulePresetItem>> Create(CreateSchedulePresetRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var rule = FindRule(request.RuleCode);
        var configuredPeriods = request.WeeklyPeriods;
        if (configuredPeriods is not null)
        {
            var periodCount = EditablePeriodCount(rule.Code);
            if (periodCount == 0)
                throw new TrackingValidationException("This report cycle does not accept custom period days.");
            if (configuredPeriods.Count != periodCount ||
                configuredPeriods.Select((period, index) => HasValidDays(rule, period, index)).Contains(false))
                throw new TrackingValidationException($"Enter {periodCount} periods with valid start and end dates for the report cycle.");
        }
        var frequency = await db.ReportingFrequencies.AsNoTracking().SingleOrDefaultAsync(x => x.Code == rule.Code, ct)
            ?? throw new TrackingValidationException("Choose one of the available report cycles.");
        var latestVersion = await db.SchedulePresets.Where(x => x.RuleCode == rule.Code).MaxAsync(x => (int?)x.Version, ct) ?? 0;
        if (latestVersion > 0 && configuredPeriods is null)
            throw new TrackingConflictException("This schedule preset has already been saved.");

        var preset = new SchedulePreset
        {
            RuleCode = rule.Code, Name = frequency.Name, DisplayOrder = frequency.DisplayOrder, Version = latestVersion + 1, MonthsPerPeriod = rule.MonthsPerPeriod,
            PeriodEndDays = rule.PeriodEndDays, DeadlineMonthOffset = rule.DeadlineMonthOffset, FixedDeadlineDay = rule.FixedDeadlineDay
        };
        if (configuredPeriods is not null)
        {
            preset.WeeklyStartDays = string.Join(',', configuredPeriods.Select(period => EncodeDay(period.StartDay, period.StartMonth)));
            preset.PeriodEndDays = string.Join(',', configuredPeriods.Select(period => EncodeDay(period.EndDay, period.EndMonth)));
        }
        db.SchedulePresets.Add(preset);
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(SchedulePreset), EntityId = preset.Id, Action = "SchedulePresetCreated",
            ActorUserId = access.UserId, DataJson = JsonSerializer.Serialize(new { preset.RuleCode, preset.Name, preset.Version })
        });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ToItem(preset));
    }

    private static ScheduleRuleDetails FindRule(string code) => Rules.SingleOrDefault(rule => rule.Code == code)
        ?? throw new TrackingValidationException("Choose one of the standard reporting schedule rules.");

    private static DateOnly FinalDeadline(ScheduleRuleDetails rule, DateOnly end)
    {
        if (rule.FixedDeadlineDay is not int day) return end;
        var deadlineMonth = end.AddMonths(rule.DeadlineMonthOffset);
        return new DateOnly(deadlineMonth.Year, deadlineMonth.Month, day);
    }

    private static SchedulePresetItem ToItem(SchedulePreset preset)
    {
        var rule = FindRule(preset.RuleCode);
        var configuredPeriods = ReadConfiguredPeriods(preset);
        var reportingPeriod = configuredPeriods is null ? rule.ReportingPeriod : string.Join(", ", configuredPeriods.Select((period, index) =>
        {
            var endDay = period.EndDay?.ToString(CultureInfo.InvariantCulture) ?? "month end";
            if (rule.MonthsPerPeriod == 1 && rule.Code != "monthly") return $"{period.StartDay}–{endDay}";

            var months = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;
            var firstMonthIndex = index * rule.MonthsPerPeriod;
            var startMonthIndex = period.StartMonth - 1 ?? firstMonthIndex;
            var endMonthIndex = period.EndMonth - 1 ?? firstMonthIndex + rule.MonthsPerPeriod - 1;
            return $"{period.StartDay} {months[startMonthIndex]}–{endDay} {months[endMonthIndex]}";
        }));
        var finalDeadline = configuredPeriods is null || rule.FixedDeadlineDay.HasValue ? rule.FinalDeadline : "Each period end";
        return new(preset.Id, preset.RuleCode, preset.Name, preset.Version, preset.DisplayOrder, reportingPeriod, finalDeadline, configuredPeriods);
    }

    private static int EditablePeriodCount(string code) => code switch
    {
        "weekly" or "quarterly" => 4,
        "twice-monthly" or "half-yearly" => 2,
        "monthly" => 12,
        "yearly" => 1,
        _ => 0
    };

    private static bool HasValidDays(ScheduleRuleDetails rule, WeeklyPresetPeriod? period, int index)
    {
        if (period is null || period.StartDay < 1 || period.EndDay is < 1 or > 31) return false;
        if (rule.Code == "yearly")
        {
            if (period.StartMonth is not (>= 1 and <= 12) || period.EndMonth is not (>= 1 and <= 12) ||
                period.EndDay is not int endDay)
                return false;
            var startMonth = period.StartMonth.Value;
            var endMonth = period.EndMonth.Value;
            return period.StartDay <= DateTime.DaysInMonth(2000, startMonth) &&
                endDay <= DateTime.DaysInMonth(2000, endMonth) &&
                (startMonth < endMonth || (startMonth == endMonth && period.StartDay <= endDay));
        }
        if (period.StartMonth.HasValue || period.EndMonth.HasValue) return false;
        if (rule.Code == "monthly")
        {
            var lastDay = DateTime.DaysInMonth(2000, index + 1);
            return period.StartDay <= lastDay &&
                (period.EndDay is null || (period.EndDay >= period.StartDay && period.EndDay <= lastDay));
        }
        if (rule.MonthsPerPeriod == 1)
            return period.StartDay <= 28 && !(period.EndDay < period.StartDay);

        var firstMonth = index * rule.MonthsPerPeriod + 1;
        return period.StartDay <= DateTime.DaysInMonth(2000, firstMonth);
    }

    private static DateOnly ResolveDate(int year, int month, int? day)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day ?? lastDay, lastDay));
    }

    private static string EncodeDay(int? day, int? month)
    {
        var value = day?.ToString(CultureInfo.InvariantCulture) ?? "end";
        return month is int number ? FormattableString.Invariant($"{number}-{value}") : value;
    }

    private static (int? Day, int? Month) DecodeDay(string value)
    {
        var parts = value.Split('-');
        var day = parts[^1] == "end" ? (int?)null : int.Parse(parts[^1], CultureInfo.InvariantCulture);
        var month = parts.Length == 2 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : (int?)null;
        return (day, month);
    }

    private static WeeklyPresetPeriod[]? ReadConfiguredPeriods(SchedulePreset? preset) => preset?.WeeklyStartDays is { } starts
        ? starts.Split(',').Zip(preset.PeriodEndDays.Split(','), (start, end) =>
        {
            var (startDay, startMonth) = DecodeDay(start);
            var (endDay, endMonth) = DecodeDay(end);
            return new WeeklyPresetPeriod(startDay!.Value, endDay, startMonth, endMonth);
        }).ToArray()
        : null;
}
