using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

internal sealed class NotificationAutomationService(ReportTrackerDbContext db, IConfiguration configuration) : INotificationAutomationService
{
    public async Task<int> PlanAsync(DateTime nowUtc, CancellationToken ct)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), TrackingRules.Dhaka);
        var policies = await db.ReminderPolicies.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var obligations = await db.TrackingObligations.Include(x => x.ReportCycle).Where(x => x.SendingStatus != SendingStatus.Sent && x.ExceptionStatus == ExceptionStatus.None).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        var created = 0;
        foreach (var obligation in obligations)
        foreach (var policy in policies.Where(p => !p.ReportDefinitionId.HasValue || p.ReportDefinitionId == obligation.ReportCycle.ReportDefinitionId))
        {
            var workingDays = policy.WorkingDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!workingDays.Contains(localNow.ToString("ddd", CultureInfo.InvariantCulture))) continue;
            if (!MatchesCategory(policy.ObligationCategory, obligation.Type)) continue;
            var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(obligation.EffectiveDueAtUtc, DateTimeKind.Utc), TrackingRules.Dhaka);
            var days = (dueLocal.Date - localNow.Date).Days;
            var leadDays = policy.LeadDaysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet();
            if (!leadDays.Contains(days) && !(policy.DailyOverdueReminders && days < 0)) continue;
            var recipientRoles = policy.RecipientsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var recipientIds = new List<Guid?>();
            if (recipientRoles.Contains("Primary")) recipientIds.Add(obligation.PrimaryOfficerId);
            if (recipientRoles.Contains("Deputy")) recipientIds.Add(obligation.DeputyOfficerId);
            foreach (var userId in recipientIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct())
            {
                if (!users.TryGetValue(userId, out var user)) continue;
                foreach (var channel in policy.ChannelsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var recipient = channel == "Email" ? user.Email : user.MobileNumber;
                    if (string.IsNullOrWhiteSpace(recipient)) continue;
                    var key = $"{obligation.Id:N}:{policy.Id:N}:{DateOnly.FromDateTime(localNow):yyyyMMdd}:{channel}:{userId:N}";
                    if (await db.NotificationJobs.AnyAsync(x => x.LogicalKey == key, ct)) continue;
                    var localDispatch = DateTime.SpecifyKind(localNow.Date + policy.DispatchTime.ToTimeSpan(), DateTimeKind.Unspecified);
                    db.NotificationJobs.Add(new NotificationJob { TrackingObligationId = obligation.Id, ReminderPolicyId = policy.Id,
                        LogicalKey = key, LocalDate = DateOnly.FromDateTime(localNow), Channel = channel, Recipient = recipient,
                        DueAtUtc = TimeZoneInfo.ConvertTimeToUtc(localDispatch, TrackingRules.Dhaka) });
                    created++;
                }
            }
        }
        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<int> DispatchAsync(DateTime nowUtc, CancellationToken ct)
    {
        var jobs = await db.NotificationJobs.Where(x => x.Status == "Pending" && x.DueAtUtc <= nowUtc && x.AttemptCount < 5)
            .OrderBy(x => x.DueAtUtc).Take(100).ToListAsync(ct);
        var policyIds = jobs.Select(x => x.ReminderPolicyId).Distinct().ToArray();
        var policies = await db.ReminderPolicies.AsNoTracking().Where(x => policyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var attempted = 0;
        foreach (var job in jobs)
        {
            if (policies.TryGetValue(job.ReminderPolicyId, out var policy) && IsQuietTime(nowUtc, policy, out var resumeAtUtc))
            {
                job.DueAtUtc = resumeAtUtc;
                continue;
            }
            var obligation = await db.TrackingObligations.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition).FirstAsync(x => x.Id == job.TrackingObligationId, ct);
            var message = $"ReportTracker reminder: {obligation.ReportCycle.ReportDefinition.Name} - {obligation.Name} is due {TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(obligation.EffectiveDueAtUtc, DateTimeKind.Utc), TrackingRules.Dhaka):dd MMM yyyy HH:mm} Asia/Dhaka.";
            var (success, reference, failure) = job.Channel == "Email"
                ? await SendEmail(job.Recipient, "ReportTracker deadline reminder", message, ct)
                : await SendSms(job.Recipient, message, ct);
            attempted++;
            job.AttemptCount++;
            job.Status = success ? "Delivered" : job.AttemptCount >= 5 ? "Failed" : "Pending";
            if (!success && job.Status == "Pending") job.DueAtUtc = nowUtc.AddMinutes(Math.Pow(2, job.AttemptCount));
            db.NotificationDeliveries.Add(new NotificationDelivery { NotificationJobId = job.Id, AttemptedAtUtc = nowUtc,
                Succeeded = success, ProviderReference = reference, FailureReason = failure });
        }
        if (jobs.Count > 0) await db.SaveChangesAsync(ct);
        return attempted;
    }

    private static bool MatchesCategory(string category, ObligationType type) => category.Equals("All", StringComparison.OrdinalIgnoreCase)
        || category.Equals("Component", StringComparison.OrdinalIgnoreCase) && type == ObligationType.Internal
        || category.Equals("Main report", StringComparison.OrdinalIgnoreCase) && type == ObligationType.External;

    private static bool IsQuietTime(DateTime nowUtc, ReminderPolicy policy, out DateTime resumeAtUtc)
    {
        resumeAtUtc = nowUtc;
        if (!policy.QuietHoursStart.HasValue || !policy.QuietHoursEnd.HasValue) return false;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), TrackingRules.Dhaka);
        var time = TimeOnly.FromDateTime(localNow);
        var start = policy.QuietHoursStart.Value;
        var end = policy.QuietHoursEnd.Value;
        var quiet = start <= end ? time >= start && time < end : time >= start || time < end;
        if (!quiet) return false;
        var resumeDate = start <= end || time < end ? localNow.Date : localNow.Date.AddDays(1);
        resumeAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(resumeDate + end.ToTimeSpan(), DateTimeKind.Unspecified), TrackingRules.Dhaka);
        return true;
    }

    private async Task<(bool Success, string? Reference, string? Failure)> SendEmail(string recipient, string subject, string body, CancellationToken ct)
    {
        var host = configuration["Notifications:Email:Host"];
        var from = configuration["Notifications:Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from)) return (false, null, "Email provider is not configured.");
        try
        {
            using var message = new MailMessage(from, recipient, subject, body);
            using var client = new SmtpClient(host, configuration.GetValue("Notifications:Email:Port", 587))
            { EnableSsl = configuration.GetValue("Notifications:Email:EnableSsl", true) };
            var username = configuration["Notifications:Email:Username"];
            if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, configuration["Notifications:Email:Password"]);
            await client.SendMailAsync(message, ct);
            return (true, $"EMAIL-{Guid.NewGuid():N}", null);
        }
        catch (Exception ex) { return (false, null, ex.Message[..Math.Min(ex.Message.Length, 500)]); }
    }

    private async Task<(bool Success, string? Reference, string? Failure)> SendSms(string recipient, string body, CancellationToken ct)
    {
        var endpoint = configuration["Notifications:Sms:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint)) return (false, null, "SMS provider is not configured.");
        try
        {
            using var client = new HttpClient();
            var apiKey = configuration["Notifications:Sms:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            using var response = await client.PostAsJsonAsync(endpoint, new { to = recipient, message = body, sender = configuration["Notifications:Sms:Sender"] }, ct);
            if (!response.IsSuccessStatusCode) return (false, null, $"SMS gateway returned {(int)response.StatusCode}.");
            return (true, response.Headers.TryGetValues("X-Message-Id", out var values) ? values.FirstOrDefault() : $"SMS-{Guid.NewGuid():N}", null);
        }
        catch (Exception ex) { return (false, null, ex.Message[..Math.Min(ex.Message.Length, 500)]); }
    }
}

internal sealed class NotificationAutomationWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<INotificationAutomationService>();
                await service.PlanAsync(DateTime.UtcNow, stoppingToken);
                await service.DispatchAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Notification automation failed."); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
