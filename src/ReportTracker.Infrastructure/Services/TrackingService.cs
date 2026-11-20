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
        var assignedIds = access.AssignedObligations().Select(x => x.Id);
        var myPending = await pending.CountAsync(x => assignedIds.Contains(x.Id), ct);
        var dueToday = await pending.CountAsync(x => x.EffectiveDueAtUtc >= start && x.EffectiveDueAtUtc < end, ct);
        var overdue = await pending.CountAsync(x => x.EffectiveDueAtUtc < now, ct);
        var evidencePending = await query.CountAsync(x => x.EvidenceStatus == EvidenceStatus.Missing || x.EvidenceStatus == EvidenceStatus.Provided || x.EvidenceStatus == EvidenceStatus.Rejected, ct);
        // Fetch all authorized rows so client filters and totals describe the same collection.
        var rows = await query.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Include(x => x.SendingEvents).OrderBy(x => x.EffectiveDueAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        var owners = await db.Users.AsNoTracking().Where(x => rows.Select(r => r.PrimaryAssigneeId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var writable = await access.AuthorizedObligations(true).Select(x => x.Id).ToHashSetAsync(ct);
        var assigned = await assignedIds.ToHashSetAsync(ct);
        var incoming = await access.AssignedReceivingObligations().Select(x => x.Id).ToHashSetAsync(ct);
        var divisionIds = rows.SelectMany(x => new Guid?[] { x.AccountableDivisionId, x.ReceivingDivisionId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var divisions = await db.Divisions.AsNoTracking().Where(x => divisionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var readiness = await LoadReadinessAsync(rows, ct);
        var readyForSubmission = rows.Count(x => x.Type == ObligationType.External && x.SendingStatus != SendingStatus.Sent &&
            readiness.TryGetValue(x.Id, out var value) && value.Completed == value.Required);
        var upcoming = rows.Select(x => ToDto(x, owners.GetValueOrDefault(x.PrimaryAssigneeId ?? Guid.Empty, "Unassigned"), writable.Contains(x.Id), now, assigned.Contains(x.Id),
            readiness.GetValueOrDefault(x.Id), divisions, incoming.Contains(x.Id))).ToList();
        var performance = Enum.GetValues<SendingStatus>().Select(status => new StatusCountDto(Label(status), rows.Count(x => x.SendingStatus == status))).ToList();
        return new(myPending, dueToday, overdue, readyForSubmission, evidencePending, upcoming, performance);
    }

    public async Task<ObligationListItemDto?> GetObligationAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var row = await access.AuthorizedObligations().AsNoTracking().Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Include(x => x.SendingEvents).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return null;
        var owner = await db.Users.Where(x => x.Id == row.PrimaryAssigneeId).Select(x => x.DisplayName).FirstOrDefaultAsync(ct);
        var divisionIds = new Guid?[] { row.AccountableDivisionId, row.ReceivingDivisionId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var divisions = await db.Divisions.AsNoTracking().Where(x => divisionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var readiness = await LoadReadinessAsync([row], ct);
        return ToDto(row, owner ?? "Unassigned", await access.AuthorizedObligations(true).AnyAsync(x => x.Id == id, ct), DateTime.UtcNow,
            await access.AssignedObligations().AnyAsync(x => x.Id == id, ct),
            readiness.GetValueOrDefault(row.Id), divisions, await access.AssignedReceivingObligations().AnyAsync(x => x.Id == id, ct));
    }

    public async Task<bool> RecordSendingAsync(Guid obligationId, RecordSendingCommand command, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        TrackingRules.ValidateSending(command, DateTime.UtcNow);
        var obligation = await access.AuthorizedObligations(true).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return false;
        if (obligation.ExceptionStatus != ExceptionStatus.None) throw new TrackingConflictException("This obligation is cancelled or not applicable.");
        if (obligation.SendingStatus == SendingStatus.Sent) throw new TrackingConflictException("Sending is already complete. Use an audited amendment to correct it.");
        if (obligation.Type == ObligationType.External)
        {
            var components = await db.TrackingObligations.AsNoTracking()
                .Where(x => x.ParentObligationId == obligation.Id && x.IsRequired && x.ExceptionStatus == ExceptionStatus.None).ToListAsync(ct);
            if (components.Any(x => !IsComplete(x)))
                throw new TrackingConflictException("Final submission is blocked until all required components are received and their required evidence is verified. Refresh to see pending components.");
        }
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
        // A new partial declaration must still advance the concurrency token, even if the status was already partial.
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

    private static ObligationListItemDto ToDto(TrackingObligation row, string owner, bool writable, DateTime now, bool assigned,
        (int Required, int Completed, IReadOnlyList<PendingComponentDto> Pending)? readiness, IReadOnlyDictionary<Guid, string> divisions, bool incoming)
    {
        var actual = row.SendingStatus == SendingStatus.Sent
            ? row.SendingEvents.Where(x => !x.IsReversed && !x.IsPartial).Select(x => (DateTime?)x.ActualSentAtUtc).Max() : null;
        return new(row.Id, row.ReportCycleId, row.ReportCycle.ReportDefinition.Name, row.ReportCycle.PeriodLabel, TrackingRules.ObligationDisplayName(row.Type, row.Name),
            row.Type switch { ObligationType.Internal => "Internal", ObligationType.External => "BB", _ => "Correction" },
            owner, DateTime.SpecifyKind(row.EffectiveDueAtUtc, DateTimeKind.Utc), TrackingRules.Timeliness(row.EffectiveDueAtUtc, actual, now),
            Label(row.SendingStatus), Label(row.ReceiptStatus), Label(row.EvidenceStatus), RecipientDisplay(row, divisions),
            Convert.ToBase64String(row.RowVersion), writable && row.SendingStatus != SendingStatus.Sent && row.ExceptionStatus == ExceptionStatus.None &&
                (row.Type != ObligationType.External || readiness is { } state && state.Required == state.Completed),
            assigned,
            readiness?.Required ?? 0, readiness?.Completed ?? 0,
            row.Type == ObligationType.External && readiness is { } value && value.Required == value.Completed,
            row.AccountableDivisionId, divisions.GetValueOrDefault(row.AccountableDivisionId, "Unknown division"), readiness?.Pending ?? [], incoming,
            row.ReportCycle.ReportDefinitionId);
    }

    private static string RecipientDisplay(TrackingObligation row, IReadOnlyDictionary<Guid, string> divisions)
    {
        if (row.Type != ObligationType.Internal) return row.Recipient;
        var receivingDivisionId = row.ReceivingDivisionId;
        if (!receivingDivisionId.HasValue && Guid.TryParse(row.Recipient, out var legacyDivisionId)) receivingDivisionId = legacyDivisionId;
        return receivingDivisionId.HasValue ? divisions.GetValueOrDefault(receivingDivisionId.Value, row.Recipient) : row.Recipient;
    }

    private async Task<Dictionary<Guid, (int Required, int Completed, IReadOnlyList<PendingComponentDto> Pending)>> LoadReadinessAsync(
        IEnumerable<TrackingObligation> obligations, CancellationToken ct)
    {
        var mainIds = obligations.Where(x => x.Type == ObligationType.External).Select(x => x.Id).ToArray();
        // Only load dependencies of final reports the caller is already authorized to read.
        var components = await db.TrackingObligations.AsNoTracking()
            .Where(x => x.ParentObligationId.HasValue && mainIds.Contains(x.ParentObligationId.Value) &&
                x.IsRequired && x.ExceptionStatus == ExceptionStatus.None)
            .OrderBy(x => x.EffectiveDueAtUtc).ThenBy(x => x.Name).ToListAsync(ct);
        var grouped = components.ToLookup(x => x.ParentObligationId!.Value);
        return mainIds.ToDictionary(
            id => id,
            id =>
            {
                var required = grouped[id].ToList();
                IReadOnlyList<PendingComponentDto> pending = required.Where(x => !IsComplete(x))
                    .Select(x => new PendingComponentDto(x.Id, x.Name, Label(x.SendingStatus), Label(x.ReceiptStatus), Label(x.EvidenceStatus))).ToList();
                return (required.Count, required.Count - pending.Count, pending);
            });
    }

    private static bool IsComplete(TrackingObligation obligation) =>
        obligation.SendingStatus == SendingStatus.Sent &&
        obligation.EvidenceStatus is EvidenceStatus.NotRequired or EvidenceStatus.Verified &&
        obligation.ReceiptStatus is ReceiptStatus.NotRequired or ReceiptStatus.ReceiptConfirmed;

    private static string Label<T>(T status) where T : Enum => status.ToString() switch
    {
        "NotSent" => "Not Sent", "PartiallySent" => "Partially Sent", "NotRequired" => "Not Required",
        "AwaitingConfirmation" => "Awaiting Confirmation", "ReceiptConfirmed" => "Receipt Confirmed", "ReceiptDisputed" => "Receipt Disputed", _ => status.ToString()
    };
}
