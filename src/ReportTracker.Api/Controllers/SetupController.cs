using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/setup")]
public sealed class SetupController(ReportTrackerDbContext db, TrackingAccess access, IPasswordHasher<AppUser> hasher) : ControllerBase
{
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.Grants("reports.manage").AnyAsync(ct) && !await access.HasAsync("administration.manage", null, null, ct)) return Forbid();
        return Ok(new
        {
            Divisions = await db.Divisions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItem(x.Id, x.Name)).ToListAsync(ct),
            Officers = await db.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayName).Select(x => new { x.Id, Name = x.DisplayName, x.DivisionId }).ToListAsync(ct)
        });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var grants = access.Grants("reports.manage");
        var reports = await db.ReportDefinitions.AsNoTracking().Where(x => grants.Any(g =>
                (!g.DivisionId.HasValue || g.DivisionId == x.OwnerDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.Id)))
            .OrderBy(x => x.Name).Select(x => new
            {
                x.Id, x.Code, x.Name, x.OwnerDivisionId, x.BbRecipient,
                Frequency = x.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.Frequency).FirstOrDefault(),
                CycleCount = db.ReportCycles.Count(c => c.ReportDefinitionId == x.Id)
            }).ToListAsync(ct);
        return Ok(reports.Select(x => new ReportItem(x.Id, x.Code, x.Name, x.OwnerDivisionId, x.BbRecipient, x.Frequency.ToString(), x.CycleCount)));
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport(CreateReportRequest request, CancellationToken ct)
    {
        await access.RequireAsync("reports.manage", request.OwnerDivisionId, null, ct);
        if (!await db.Divisions.AnyAsync(x => x.Id == request.OwnerDivisionId && x.IsActive, ct)) throw new TrackingValidationException("Choose an active owner division.");
        if (!Enum.TryParse<Frequency>(request.Frequency, out var frequency) || !Enum.IsDefined(frequency)) throw new TrackingValidationException("Choose a valid reporting frequency.");
        var report = new ReportDefinition { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), Description = request.Description?.Trim(),
            OwnerDivisionId = request.OwnerDivisionId, BbRecipient = request.BbRecipient.Trim(), InstructionReference = request.InstructionReference?.Trim() };
        report.Versions.Add(new DefinitionVersion { ReportDefinitionId = report.Id, VersionNumber = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            Frequency = frequency, DueTime = new TimeOnly(17, 0), TimeZoneId = "Asia/Dhaka" });
        db.ReportDefinitions.Add(report);
        Audit(nameof(ReportDefinition), report.Id, "ReportCreated", new { report.Code, report.Name });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/reports/{report.Id}", new { report.Id });
    }

    [HttpPost("reports/{reportId:guid}/cycles")]
    public async Task<IActionResult> CreateCycle(Guid reportId, CreateCycleRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        if (request.PeriodStart == default || request.PeriodEnd < request.PeriodStart) throw new TrackingValidationException("Enter a valid reporting period.");
        if (!request.Obligations.Any(x => x.Type == "BB")) throw new TrackingValidationException("Include at least one final BB submission obligation.");
        var version = report.Versions.OrderByDescending(x => x.VersionNumber).First();
        var cycle = new ReportCycle { ReportDefinitionId = reportId, DefinitionVersionId = version.Id, PeriodKey = request.PeriodKey.Trim(),
            PeriodLabel = request.PeriodLabel.Trim(), PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd };
        foreach (var item in request.Obligations)
        {
            if (item.Type is not ("Internal" or "BB") || item.DueAtUtc.Kind != DateTimeKind.Utc || item.DueAtUtc < new DateTime(2000, 1, 1))
                throw new TrackingValidationException("Each obligation needs a valid type and deadline in UTC.");
            if (!await db.Divisions.AnyAsync(x => x.Id == item.DivisionId && x.IsActive, ct) ||
                !await db.Users.AnyAsync(x => x.Id == item.PrimaryOfficerId && x.DivisionId == item.DivisionId && x.IsActive, ct) ||
                (item.DeputyOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == item.DeputyOfficerId && x.DivisionId == item.DivisionId && x.IsActive, ct)))
                throw new TrackingValidationException("Primary and deputy officers must be active members of the accountable division.");
            cycle.Obligations.Add(new TrackingObligation { ReportCycleId = cycle.Id, Type = item.Type == "BB" ? ObligationType.External : ObligationType.Internal,
                Name = item.Name.Trim(), AccountableDivisionId = item.DivisionId, PrimaryOfficerId = item.PrimaryOfficerId, DeputyOfficerId = item.DeputyOfficerId,
                Recipient = item.Recipient.Trim(), OriginalDueAtUtc = item.DueAtUtc, EffectiveDueAtUtc = item.DueAtUtc,
                ReceiptStatus = item.ReceiptRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                EvidenceStatus = item.EvidenceRequired ? EvidenceStatus.Missing : EvidenceStatus.NotRequired });
        }
        db.ReportCycles.Add(cycle);
        Audit(nameof(ReportCycle), cycle.Id, "CycleCreated", new { reportId, cycle.PeriodKey, ObligationCount = cycle.Obligations.Count });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/reports/{reportId}/cycles/{cycle.Id}", new { cycle.Id });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var users = await db.Users.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(ct);
        var roles = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id
                           select new { assignment.UserId, role.Name }).ToListAsync(ct);
        return Ok(users.Select(x => new UserItem(x.Id, x.EmployeeId, x.DisplayName, x.Email, x.DivisionId, x.IsActive,
            roles.Where(r => r.UserId == x.Id).Select(r => r.Name).ToArray())));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        if (!await db.Divisions.AnyAsync(x => x.Id == request.DivisionId && x.IsActive, ct)) throw new TrackingValidationException("Choose an active division.");
        var user = new AppUser { EmployeeId = request.EmployeeId.Trim(), DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(), DivisionId = request.DivisionId };
        db.Users.Add(user);
        db.LocalCredentials.Add(new LocalCredential { UserId = user.Id, PasswordHash = hasher.HashPassword(user, request.Password) });
        if (request.IsCoordinator)
        {
            var role = await db.Roles.SingleAsync(x => x.Name == "Report Coordinator", ct);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, DivisionId = request.DivisionId });
        }
        Audit(nameof(AppUser), user.Id, "UserCreated", new { user.EmployeeId, user.DivisionId, request.IsCoordinator });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/users/{user.Id}", new { user.Id });
    }

    [HttpPost("divisions")]
    public async Task<IActionResult> CreateDivision(CreateDivisionRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var division = new Division { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim() };
        db.Divisions.Add(division);
        Audit(nameof(Division), division.Id, "DivisionCreated", new { division.Code, division.Name });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/divisions/{division.Id}", new { division.Id });
    }

    private void Audit(string type, Guid id, string action, object data) => db.AuditEvents.Add(new AuditEvent
    { EntityType = type, EntityId = id, Action = action, ActorUserId = access.UserId, DataJson = JsonSerializer.Serialize(data) });
}
