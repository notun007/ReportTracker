using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

internal sealed class TrackingService(ReportTrackerDbContext db, TrackingAccess access) : ITrackingService
{
    public async Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var now = DateTime.UtcNow;
        var today = TimeZoneInfo.ConvertTimeFromUtc(now, TrackingRules.Dhaka).Date;
        var start = TimeZoneInfo.ConvertTimeToUtc(today, TrackingRules.Dhaka);
        var end = start.AddDays(1);
        var query = access.AuthorizedObligations().AsNoTracking().Where(x => x.ExceptionStatus == ExceptionStatus.None);
        var pending = query.Where(x => x.SendingStatus != SendingStatus.Sent);
        var myPending = await pending.CountAsync(x => x.PrimaryOfficerId == access.UserId || x.DeputyOfficerId == access.UserId, ct);
        var dueToday = await pending.CountAsync(x => x.EffectiveDueAtUtc >= start && x.EffectiveDueAtUtc < end, ct);
        var overdue = await pending.CountAsync(x => x.EffectiveDueAtUtc < now, ct);
        var evidencePending = await query.CountAsync(x => x.EvidenceStatus == EvidenceStatus.Missing || x.EvidenceStatus == EvidenceStatus.Provided || x.EvidenceStatus == EvidenceStatus.Rejected, ct);
        // Fetch all authorized rows so client filters and totals describe the same collection.
        var rows = await query.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Include(x => x.SendingEvents).OrderBy(x => x.EffectiveDueAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        var owners = await db.Users.AsNoTracking().Where(x => rows.Select(r => r.PrimaryOfficerId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var writable = await access.AuthorizedObligations(true).Select(x => x.Id).ToHashSetAsync(ct);
        var upcoming = rows.Select(x => ToDto(x, owners.GetValueOrDefault(x.PrimaryOfficerId ?? Guid.Empty, "Unassigned"), writable.Contains(x.Id), now, access.UserId)).ToList();
        var performance = Enum.GetValues<SendingStatus>().Select(status => new StatusCountDto(Label(status), rows.Count(x => x.SendingStatus == status))).ToList();
        return new(myPending, dueToday, overdue, evidencePending, upcoming, performance);
    }

    public async Task<ObligationListItemDto?> GetObligationAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var row = await access.AuthorizedObligations().AsNoTracking().Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Include(x => x.SendingEvents).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;
        var owner = await db.Users.Where(x => x.Id == row.PrimaryOfficerId).Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        return ToDto(row, owner ?? "Unassigned", await access.AuthorizedObligations(true).AnyAsync(x => x.Id == id, ct), DateTime.UtcNow, access.UserId);
    }

    public async Task<bool> RecordSendingAsync(Guid obligationId, RecordSendingCommand command, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        TrackingRules.ValidateSending(command, DateTime.UtcNow);
        var obligation = await access.AuthorizedObligations(true).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return false;
        if (obligation.ExceptionStatus != ExceptionStatus.None) throw new TrackingConflictException("This obligation is cancelled or not applicable.");
        if (obligation.SendingStatus == SendingStatus.Sent) throw new TrackingConflictException("Sending is already complete. Use an audited amendment to correct it.");
        db.Entry(obligation).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(command.RowVersion);
        var sending = new SendingEvent
        {
            TrackingObligationId = obligationId, ActingUserId = access.UserId,
            ActualSentAtUtc = command.ActualSentAtUtc, Recipient = command.Recipient.Trim(),
            Channel = command.Channel, Reference = command.Reference?.Trim(), IsPartial = command.IsPartial,
            ScopeNames = command.ScopeNames is { Count: > 0 } ? string.Join("; ", command.ScopeNames.Select(x => x.Trim())) : null,
            TrackingRemarks = command.TrackingRemarks?.Trim()
        };
        obligation.SendingStatus = command.IsPartial ? SendingStatus.PartiallySent : SendingStatus.Sent;
        if (obligation.ReceiptStatus != ReceiptStatus.NotRequired) obligation.ReceiptStatus = ReceiptStatus.AwaitingConfirmation;
        // A new partial declaration must still advance rowversion, even if the status was already partial.
        obligation.UpdatedAtUtc = DateTime.UtcNow;
        db.SendingEvents.Add(sending);
        db.AuditEvents.Add(new AuditEvent
        {
            EntityType = nameof(TrackingObligation), EntityId = obligationId, Action = "SendingRecorded", ActorUserId = access.UserId,
            DataJson = JsonSerializer.Serialize(new { sending.Id, sending.ActualSentAtUtc, sending.Channel, sending.Reference, sending.IsPartial, sending.ScopeNames })
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SendingHistoryDto>?> GetHistoryAsync(Guid obligationId, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.AuthorizedObligations().AnyAsync(x => x.Id == obligationId, ct)) return null;
        var history = await (from sending in db.SendingEvents.AsNoTracking()
                      join user in db.Users on sending.ActingUserId equals user.Id
                      where sending.TrackingObligationId == obligationId
                      orderby sending.CreatedAtUtc descending
                      select new SendingHistoryDto(sending.Id, sending.ActualSentAtUtc, sending.CreatedAtUtc, user.DisplayName,
                          sending.Recipient, sending.Channel, sending.Reference, sending.IsPartial, sending.ScopeNames, sending.TrackingRemarks)).ToListAsync(ct);
        return history.Select(x => x with
        {
            ActualSentAtUtc = DateTime.SpecifyKind(x.ActualSentAtUtc, DateTimeKind.Utc),
            RecordedAtUtc = DateTime.SpecifyKind(x.RecordedAtUtc, DateTimeKind.Utc)
        }).ToList();
    }

    private static ObligationListItemDto ToDto(TrackingObligation row, string owner, bool writable, DateTime now, Guid userId)
    {
        var actual = row.SendingStatus == SendingStatus.Sent
            ? row.SendingEvents.Where(x => !x.IsReversed && !x.IsPartial).Select(x => (DateTime?)x.ActualSentAtUtc).Max() : null;
        return new(row.Id, row.ReportCycle.ReportDefinition.Name, row.ReportCycle.PeriodLabel, row.Name,
            row.Type switch { ObligationType.Internal => "Internal", ObligationType.External => "BB", _ => "Correction" },
            owner, DateTime.SpecifyKind(row.EffectiveDueAtUtc, DateTimeKind.Utc), TrackingRules.Timeliness(row.EffectiveDueAtUtc, actual, now),
            Label(row.SendingStatus), Label(row.ReceiptStatus), Label(row.EvidenceStatus), row.Recipient,
            Convert.ToBase64String(row.RowVersion), writable && row.SendingStatus != SendingStatus.Sent && row.ExceptionStatus == ExceptionStatus.None,
            row.PrimaryOfficerId == userId || row.DeputyOfficerId == userId);
    }

    private static string Label<T>(T status) where T : Enum => status.ToString() switch
    {
        "NotSent" => "Not Sent", "PartiallySent" => "Partially Sent", "NotRequired" => "Not Required",
        "AwaitingConfirmation" => "Awaiting Confirmation", "ReceiptConfirmed" => "Receipt Confirmed", "ReceiptDisputed" => "Receipt Disputed", _ => status.ToString()
    };
}
