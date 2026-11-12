using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public static class ComponentValidation
{
    public static IReadOnlyList<ComponentContributorItem> Contributors(string? json, Guid? primary, Guid? deputy)
    {
        if (json is not null) return JsonSerializer.Deserialize<ComponentContributorItem[]>(json) ?? [];
        var rows = new List<ComponentContributorItem>();
        if (primary.HasValue) rows.Add(new(primary.Value, "LeadPreparer", true));
        if (deputy.HasValue && deputy != primary) rows.Add(new(deputy.Value, "BackupPreparer", true));
        return rows;
    }

    public static async Task<(Guid? Primary, Guid? Deputy)> ValidateContributorsAsync(
        ReportTrackerDbContext db, Guid divisionId, IReadOnlyList<ComponentContributorItem> contributors, CancellationToken ct)
    {
        if (contributors.Count is < 1 or > 200 || contributors.Any(x => x is null ||
            x.Role is not ("LeadPreparer" or "Preparer" or "BackupPreparer" or "Reviewer")) ||
            contributors.Select(x => (x.UserId, x.Role)).Distinct().Count() != contributors.Count ||
            contributors.Count(x => x.Role == "LeadPreparer") != 1)
            throw new TrackingValidationException("Assign each role at most once per team member and choose exactly one lead preparer.");
        var members = contributors.GroupBy(x => x.UserId).ToArray();
        if (members.Length > 50) throw new TrackingValidationException("Choose up to 50 team members.");
        var handover = members.Where(member => member.Any(x => x.RecordsHandover))
            .OrderByDescending(member => member.Any(x => x.Role == "LeadPreparer"))
            .Select(member => (Guid?)member.Key).ToArray();
        if (handover.Length is < 1 or > 2)
            throw new TrackingValidationException("Designate one or two team members to record the handover.");
        var ids = members.Select(member => member.Key).ToArray();
        if (await db.Users.CountAsync(x => ids.Contains(x.Id) && x.DivisionId == divisionId && x.IsActive, ct) != ids.Length)
            throw new TrackingValidationException("All contributors must be active team members of the preparing division.");
        return (handover[0], handover.ElementAtOrDefault(1));
    }

    public static async Task ValidateAsync(ReportTrackerDbContext db, string name, string? description, int sequence,
        Guid preparingDivisionId, Guid receivingDivisionId, Guid? primaryOfficerId, Guid? deputyOfficerId, int leadDays, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 250 || description?.Length > 1000 || sequence is < 0 or > 10000 || leadDays is < 0 or > 365)
            throw new TrackingValidationException("Enter a component name, display order from 0 to 10000 and deadline lead days from 0 to 365.");
        if (!await db.Divisions.AnyAsync(x => x.Id == preparingDivisionId && x.IsActive, ct) ||
            !await db.Divisions.AnyAsync(x => x.Id == receivingDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("Choose active preparing and receiving divisions.");
        if (!primaryOfficerId.HasValue || !await db.Users.AnyAsync(x => x.Id == primaryOfficerId && x.DivisionId == preparingDivisionId && x.IsActive, ct) ||
            (deputyOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == deputyOfficerId && x.DivisionId == preparingDivisionId && x.IsActive, ct)))
            throw new TrackingValidationException("Component officers must be active members of the preparing division, with a primary officer assigned.");
        if (primaryOfficerId == deputyOfficerId) throw new TrackingValidationException("Choose a different deputy preparing officer.");
    }
}
