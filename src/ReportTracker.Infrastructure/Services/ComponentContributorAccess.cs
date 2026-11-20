using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Infrastructure.Services;

internal static class ComponentContributorAccess
{
    public static IQueryable<TrackingObligation> AssignedComponents(ReportTrackerDbContext db, Guid userId)
    {
        if (db.Database.IsOracle())
        {
            // Query the period snapshot, never the current component definition. TO_CLOB handles
            // the existing NCLOB column without truncating contributor lists. Keep the actor bound.
            var actor = userId.ToString();
            return db.TrackingObligations.FromSqlInterpolated($"""
                SELECT o.* FROM "rt_tracking_obligations" o
                WHERE JSON_EXISTS(TO_CLOB(o."contributors_json"),
                    '$[*]?(@.UserId == $actor && (@.Role == "LeadPreparer" || @.Role == "Preparer" || @.Role == "BackupPreparer" || @.Role == "Reviewer"))'
                    PASSING CAST({actor} AS VARCHAR2(36)) AS "actor" FALSE ON ERROR)
                """).Where(x => x.Type == ObligationType.Internal);
        }

        // The in-memory test provider cannot execute Oracle JSON predicates.
        return db.TrackingObligations.Where(x => x.Type == ObligationType.Internal && HasAssignment(x.ContributorsJson, userId));
    }

    private static bool HasAssignment(string? json, Guid userId)
    {
        if (json is null || userId == Guid.Empty) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.Object && x.TryGetProperty("UserId", out var id) &&
                id.ValueKind == JsonValueKind.String && id.GetString() == userId.ToString() &&
                x.TryGetProperty("Role", out var role) && role.ValueKind == JsonValueKind.String &&
                role.GetString() is "LeadPreparer" or "Preparer" or "BackupPreparer" or "Reviewer");
        }
        catch (JsonException)
        {
            // Invalid historical metadata must never create an authorization grant.
            return false;
        }
    }
}
