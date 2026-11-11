using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

public static class ComponentValidation
{
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
