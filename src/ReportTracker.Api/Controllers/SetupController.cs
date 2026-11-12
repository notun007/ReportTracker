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
                x.Id, x.Code, x.Name, x.OwnerDivisionId, x.SubmittingDivisionId, x.ReceivingDivisionId, x.BbRecipient,
                x.SubmissionRecipientId, x.InstructionReference, x.Description,
                RecipientName = x.SubmissionRecipient != null ? x.SubmissionRecipient.Name : x.BbRecipient,
                Departments = x.SubmissionDepartments.Select(d => new { d.DepartmentId, d.Department.Name }).ToList(),
                Version = x.Versions.OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.Frequency, v.DueTime, v.ComponentDeadlineOffsetDays, v.TimeZoneId }).FirstOrDefault(),
                CycleCount = db.ReportCycles.Count(c => c.ReportDefinitionId == x.Id)
            }).ToListAsync(ct);
        var ids = reports.Select(x => x.Id).ToArray();
        var now = DateTime.UtcNow;
        var assignments = await db.ResponsibilityAssignments.AsNoTracking().Where(x => ids.Contains(x.ReportDefinitionId) && x.ResponsibilityType == "MainSender" &&
            x.EffectiveFromUtc <= now && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now)).OrderByDescending(x => x.EffectiveFromUtc).ToListAsync(ct);
        return Ok(reports.Select(x => { var sender = assignments.FirstOrDefault(a => a.ReportDefinitionId == x.Id && a.DivisionId == x.SubmittingDivisionId);
            return new ReportItem(x.Id, x.Code, x.Name, x.OwnerDivisionId, x.BbRecipient, (x.Version?.Frequency ?? default).ToString(), x.CycleCount,
                x.SubmittingDivisionId, sender?.PrimaryOfficerId, sender?.DeputyOfficerId, x.ReceivingDivisionId,
                x.SubmissionRecipientId, x.Departments.Select(d => d.DepartmentId).ToArray(),
                ReportDefinition.FormatSubmissionDestination(x.RecipientName, x.Departments.Select(d => d.Name)),
                x.Version?.DueTime, x.Version?.ComponentDeadlineOffsetDays, x.Version?.TimeZoneId,
                x.InstructionReference, x.Description); }));
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport(CreateReportRequest request, CancellationToken ct)
    {
        await access.RequireAsync("reports.manage", request.OwnerDivisionId, null, ct);
        var recipient = request.SubmissionRecipientId is Guid recipientId
            ? await ValidateDestination(recipientId, request.SubmissionDepartmentIds, ct)
            : null;
        if (recipient is null && (string.IsNullOrWhiteSpace(request.BbRecipient) || request.SubmissionDepartmentIds is { Count: > 0 }))
            throw new TrackingValidationException("Choose a submission recipient and its departments.");
        await ValidateResponsibilities(new ReportResponsibilitiesRequest(request.OwnerDivisionId, request.SubmittingDivisionId ?? Guid.Empty,
            request.PrimarySubmittingOfficerId ?? Guid.Empty, request.DeputySubmittingOfficerId), ct);
        if (!Enum.TryParse<Frequency>(request.Frequency, out var frequency) || !Enum.IsDefined(frequency)) throw new TrackingValidationException("Choose a valid reporting frequency.");
        if (request.ComponentDeadlineOffsetDays is < 0 or > 365) throw new TrackingValidationException("Deadline lead days must be between 0 and 365 days.");
        if (!request.ReceivingDivisionId.HasValue || !await db.Divisions.AnyAsync(x => x.Id == request.ReceivingDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("Choose an active receiving division for the report's components.");
        var components = (request.Components ?? []).ToList();
        if (components.Count > 100) throw new TrackingValidationException("A report can contain at most 100 components.");
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component is null) throw new TrackingValidationException("Every component must contain its configuration.");
            if (component.Contributors is not null)
            {
                var (primary, deputy) = await ComponentValidation.ValidateContributorsAsync(db, component.ProviderDivisionId, component.Contributors, ct);
                components[i] = component = component with { PrimaryOfficerId = primary!.Value, DeputyOfficerId = deputy };
            }
            await ComponentValidation.ValidateAsync(db, component.Name, component.Description, component.Sequence, component.ProviderDivisionId,
                request.ReceivingDivisionId.Value, component.PrimaryOfficerId, component.DeputyOfficerId, request.ComponentDeadlineOffsetDays, ct);
        }
        if (components.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != components.Count)
            throw new TrackingValidationException("Component names must be unique within a report.");
        var report = new ReportDefinition { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), Description = request.Description?.Trim(),
            OwnerDivisionId = request.OwnerDivisionId, SubmittingDivisionId = request.SubmittingDivisionId, ReceivingDivisionId = request.ReceivingDivisionId,
            BbRecipient = recipient?.Name ?? request.BbRecipient!.Trim(), SubmissionRecipientId = recipient?.Id,
            InstructionReference = request.InstructionReference?.Trim() };
        if (recipient is not null)
            foreach (var departmentId in request.SubmissionDepartmentIds!)
                report.SubmissionDepartments.Add(new ReportSubmissionDepartment { ReportDefinitionId = report.Id, DepartmentId = departmentId });
        var version = new DefinitionVersion { ReportDefinitionId = report.Id, VersionNumber = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            Frequency = frequency, DueTime = request.DueTime ?? new TimeOnly(17, 0),
            ComponentDeadlineOffsetDays = request.ComponentDeadlineOffsetDays, TimeZoneId = "Asia/Dhaka" };
        report.Versions.Add(version);
        db.ReportDefinitions.Add(report);
        foreach (var item in components)
        {
            var component = new ComponentDefinition { DefinitionVersionId = version.Id, Name = item.Name.Trim(), Description = item.Description?.Trim(),
                IsRequired = item.IsRequired, Sequence = item.Sequence, ProviderDivisionId = item.ProviderDivisionId,
                PrimaryOfficerId = item.PrimaryOfficerId, DeputyOfficerId = item.DeputyOfficerId, ReceivingDivisionId = request.ReceivingDivisionId.Value,
                ContributorsJson = JsonSerializer.Serialize(item.Contributors ?? ComponentValidation.Contributors(null, item.PrimaryOfficerId, item.DeputyOfficerId)),
                ReceiptConfirmationRequired = item.ReceiptRequired, EvidenceRequired = item.EvidenceRequired,
                DeadlineRuleJson = JsonSerializer.Serialize(new { offsetDays = request.ComponentDeadlineOffsetDays }) };
            db.ComponentDefinitions.Add(component);
            Audit(nameof(ComponentDefinition), component.Id, "ComponentCreated", new { reportId = report.Id, component.Name, component.ProviderDivisionId, component.IsRequired });
        }
        db.ResponsibilityAssignments.Add(new ResponsibilityAssignment { ReportDefinitionId = report.Id, DivisionId = request.SubmittingDivisionId!.Value,
            PrimaryOfficerId = request.PrimarySubmittingOfficerId!.Value, DeputyOfficerId = request.DeputySubmittingOfficerId,
            ResponsibilityType = "MainSender", EffectiveFromUtc = DateTime.UtcNow });
        Audit(nameof(ReportDefinition), report.Id, "ReportCreated", new { report.Code, report.Name, report.SubmissionRecipientId, request.SubmissionDepartmentIds });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/reports/{report.Id}", new { report.Id });
    }

    [HttpPut("reports/{reportId:guid}/submission-destination")]
    public async Task<IActionResult> SetSubmissionDestination(Guid reportId, SetSubmissionDestinationRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.Include(x => x.SubmissionDepartments)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        var recipient = await ValidateDestination(request.SubmissionRecipientId, request.SubmissionDepartmentIds, ct);
        var previousRecipientId = report.SubmissionRecipientId;
        var previousDepartmentIds = report.SubmissionDepartments.Select(x => x.DepartmentId).ToArray();
        report.SubmissionRecipientId = recipient.Id;
        report.BbRecipient = recipient.Name;
        var selected = request.SubmissionDepartmentIds.ToHashSet();
        db.ReportSubmissionDepartments.RemoveRange(report.SubmissionDepartments.Where(x => !selected.Contains(x.DepartmentId)).ToArray());
        foreach (var departmentId in selected.Except(previousDepartmentIds))
            report.SubmissionDepartments.Add(new ReportSubmissionDepartment { ReportDefinitionId = report.Id, DepartmentId = departmentId });
        Audit(nameof(ReportDefinition), report.Id, "SubmissionDestinationChanged", new
        {
            previousRecipientId, previousDepartmentIds, report.SubmissionRecipientId, request.SubmissionDepartmentIds
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<SubmissionRecipient> ValidateDestination(Guid recipientId, IReadOnlyList<Guid>? departmentIds, CancellationToken ct)
    {
        var recipient = await db.SubmissionRecipients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recipientId, ct);
        if (recipient is null) throw new TrackingValidationException("Choose a valid submission recipient.");
        if (departmentIds is not { Count: >= 1 and <= 20 } || departmentIds.Distinct().Count() != departmentIds.Count)
            throw new TrackingValidationException("Select between 1 and 20 distinct departments.");
        var matched = await db.SubmissionRecipientDepartments.CountAsync(x => x.SubmissionRecipientId == recipientId && departmentIds.Contains(x.Id), ct);
        if (matched != departmentIds.Count) throw new TrackingValidationException("All selected departments must belong to the submission recipient.");
        return recipient;
    }

    [HttpPut("reports/{reportId:guid}/responsibilities")]
    public async Task<IActionResult> SetResponsibilities(Guid reportId, ReportResponsibilitiesRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        if (request.CoordinatingDivisionId != report.OwnerDivisionId)
            await access.RequireAsync("reports.manage", request.CoordinatingDivisionId, reportId, ct);
        await ValidateResponsibilities(request, ct);
        var now = DateTime.UtcNow;
        var assignments = await db.ResponsibilityAssignments.Where(x => x.ReportDefinitionId == reportId && x.ResponsibilityType == "MainSender" &&
            (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now)).ToListAsync(ct);
        foreach (var assignment in assignments) assignment.EffectiveToUtc = now;
        var previousCoordinating = report.OwnerDivisionId; var previousSubmitting = report.SubmittingDivisionId;
        report.OwnerDivisionId = request.CoordinatingDivisionId; report.SubmittingDivisionId = request.SubmittingDivisionId;
        db.ResponsibilityAssignments.Add(new ResponsibilityAssignment { ReportDefinitionId = reportId, DivisionId = request.SubmittingDivisionId,
            PrimaryOfficerId = request.PrimarySubmittingOfficerId, DeputyOfficerId = request.DeputySubmittingOfficerId,
            ResponsibilityType = "MainSender", EffectiveFromUtc = now });
        Audit(nameof(ReportDefinition), reportId, "ReportResponsibilitiesChanged", new { previousCoordinating, previousSubmitting, request });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ValidateResponsibilities(ReportResponsibilitiesRequest request, CancellationToken ct)
    {
        if (!await db.Divisions.AnyAsync(x => x.Id == request.CoordinatingDivisionId && x.IsActive, ct) ||
            !await db.Divisions.AnyAsync(x => x.Id == request.SubmittingDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("Choose active coordinating and final submitting divisions.");
        if (!await db.Users.AnyAsync(x => x.Id == request.PrimarySubmittingOfficerId && x.DivisionId == request.SubmittingDivisionId && x.IsActive, ct) ||
            (request.DeputySubmittingOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == request.DeputySubmittingOfficerId && x.DivisionId == request.SubmittingDivisionId && x.IsActive, ct)))
            throw new TrackingValidationException("Final submitting officers must be active members of the final submitting division.");
        if (request.DeputySubmittingOfficerId == request.PrimarySubmittingOfficerId)
            throw new TrackingValidationException("Choose a different deputy submitting officer.");
    }

    [HttpGet("reports/{reportId:guid}/cycles")]
    public async Task<ActionResult<IReadOnlyList<ReportingCycleItem>>> Cycles(Guid reportId, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        var cycles = await db.ReportCycles.AsNoTracking().Where(x => x.ReportDefinitionId == reportId)
            .Include(x => x.Obligations).OrderByDescending(x => x.PeriodStart).ThenBy(x => x.PeriodKey).ToListAsync(ct);
        var obligations = cycles.SelectMany(x => x.Obligations).ToList();
        var officerIds = obligations.SelectMany(x => new[] { x.PrimaryOfficerId, x.DeputyOfficerId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var divisionIds = obligations.Select(x => x.AccountableDivisionId).Distinct().ToArray();
        var officers = await db.Users.AsNoTracking().Where(x => officerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var divisions = await db.Divisions.AsNoTracking().Where(x => divisionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Ok(cycles.Select(cycle => new ReportingCycleItem(cycle.Id, cycle.PeriodKey, cycle.PeriodLabel, cycle.PeriodStart, cycle.PeriodEnd,
            cycle.Obligations.OrderBy(x => x.Type == ObligationType.External ? 0 : 1).ThenBy(x => x.Name).Select(x =>
                new CycleObligationItem(TrackingRules.ObligationDisplayName(x.Type, x.Name), x.Type == ObligationType.External ? "BB" : x.Type.ToString(),
                    x.AccountableDivisionId, divisions.GetValueOrDefault(x.AccountableDivisionId, "Unknown division"),
                    x.PrimaryOfficerId, officers.GetValueOrDefault(x.PrimaryOfficerId ?? Guid.Empty),
                    x.DeputyOfficerId, officers.GetValueOrDefault(x.DeputyOfficerId ?? Guid.Empty), x.Recipient,
                    x.EffectiveDueAtUtc, x.ReceiptStatus != ReceiptStatus.NotRequired, x.EvidenceStatus != EvidenceStatus.NotRequired, x.ReceivingDivisionId,
                    x.Type == ObligationType.Internal ? ComponentValidation.Contributors(x.ContributorsJson, x.PrimaryOfficerId, x.DeputyOfficerId) : null)).ToList())).ToList());
    }

    [HttpPost("reports/{reportId:guid}/cycles")]
    public async Task<IActionResult> CreateCycle(Guid reportId, CreateCycleRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.Include(x => x.Versions).Include(x => x.SubmissionRecipient)
            .Include(x => x.SubmissionDepartments).ThenInclude(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        if (request.PeriodStart == default || request.PeriodEnd < request.PeriodStart) throw new TrackingValidationException("Enter a valid reporting period.");
        if (!request.Obligations.Any(x => x.Type == "BB")) throw new TrackingValidationException("Include at least one final submission obligation.");
        var version = report.Versions.OrderByDescending(x => x.VersionNumber).First();
        var cycle = new ReportCycle { ReportDefinitionId = reportId, DefinitionVersionId = version.Id, PeriodKey = request.PeriodKey.Trim(),
            PeriodLabel = request.PeriodLabel.Trim(), PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd };
        if (request.Obligations.Count(x => x.Type == "BB") != 1)
            throw new TrackingValidationException("Include exactly one final submission obligation.");
        var mainRequest = request.Obligations.Single(x => x.Type == "BB");
        if (!report.SubmittingDivisionId.HasValue || mainRequest.DivisionId != report.SubmittingDivisionId)
            throw new TrackingValidationException("The final submission must use the report's configured final submitting division. Update Report responsibilities first.");
        if (request.Obligations.Any(x => x.Type == "Internal" && x.DueAtUtc > mainRequest.DueAtUtc))
            throw new TrackingValidationException("Component deadlines cannot be later than the final report deadline.");

        var obligations = new List<TrackingObligation>();
        foreach (var item in request.Obligations.OrderByDescending(x => x.Type == "BB"))
        {
            var receivingDivision = item.Type == "Internal" ? report.ReceivingDivisionId ?? item.ReceivingDivisionId : item.ReceivingDivisionId;
            if (item.Type == "Internal" && report.ReceivingDivisionId.HasValue && item.ReceivingDivisionId.HasValue && item.ReceivingDivisionId != report.ReceivingDivisionId)
                throw new TrackingValidationException("Internal tasks must use the report's receiving division.");
            if (item.Type == "Internal" && item.ReceiptRequired && !receivingDivision.HasValue)
                throw new TrackingValidationException("Choose a receiving division for internal receipt confirmation.");
            if (receivingDivision.HasValue && (item.Type != "Internal" ||
                !await db.Divisions.AnyAsync(x => x.Id == receivingDivision && x.IsActive, ct)))
                throw new TrackingValidationException("Choose an active receiving division for an internal task.");
            if (item.Type is not ("Internal" or "BB") || item.DueAtUtc.Kind != DateTimeKind.Utc || item.DueAtUtc < new DateTime(2000, 1, 1))
                throw new TrackingValidationException("Each obligation needs a valid type and deadline in UTC.");
            if (!await db.Divisions.AnyAsync(x => x.Id == item.DivisionId && x.IsActive, ct) ||
                !await db.Users.AnyAsync(x => x.Id == item.PrimaryOfficerId && x.DivisionId == item.DivisionId && x.IsActive, ct) ||
                (item.DeputyOfficerId.HasValue && !await db.Users.AnyAsync(x => x.Id == item.DeputyOfficerId && x.DivisionId == item.DivisionId && x.IsActive, ct)))
                throw new TrackingValidationException("Primary and deputy officers must be active members of the accountable division.");
            var obligation = new TrackingObligation { ReportCycleId = cycle.Id, Type = item.Type == "BB" ? ObligationType.External : ObligationType.Internal,
                Name = item.Name.Trim(), AccountableDivisionId = item.DivisionId, PrimaryOfficerId = item.PrimaryOfficerId, DeputyOfficerId = item.DeputyOfficerId,
                Recipient = item.Type == "BB" && report.SubmissionRecipientId.HasValue ? report.SubmissionDestination() : item.Recipient.Trim(),
                ReceivingDivisionId = receivingDivision, OriginalDueAtUtc = item.DueAtUtc, EffectiveDueAtUtc = item.DueAtUtc,
                ReceiptStatus = item.ReceiptRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                EvidenceStatus = item.EvidenceRequired ? EvidenceStatus.Missing : EvidenceStatus.NotRequired };
            obligations.Add(obligation);
            cycle.Obligations.Add(obligation);
        }
        var main = obligations.Single(x => x.Type == ObligationType.External);
        foreach (var component in obligations.Where(x => x.Type == ObligationType.Internal)) component.ParentObligationId = main.Id;
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
        return Ok(users.Select(x => new UserItem(x.Id, x.EmployeeId, x.DisplayName, x.Email, x.MobileNumber, x.DivisionId, x.IsActive,
            roles.Where(r => r.UserId == x.Id).Select(r => r.Name).ToArray())));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        if (!await db.Divisions.AnyAsync(x => x.Id == request.DivisionId && x.IsActive, ct)) throw new TrackingValidationException("Choose an active division.");
        var user = new AppUser { EmployeeId = request.EmployeeId.Trim(), DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(), MobileNumber = request.MobileNumber?.Trim(), DivisionId = request.DivisionId };
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

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IActionResult> ChangeUserStatus(Guid userId, ChangeUserStatusRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        if (userId == access.UserId && !request.IsActive) throw new TrackingConflictException("You cannot deactivate your own active session.");
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        user.IsActive = request.IsActive;
        Audit(nameof(AppUser), userId, request.IsActive ? "UserActivated" : "UserDeactivated", new { user.EmployeeId });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, ResetPasswordRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        var credential = await db.LocalCredentials.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential is null) db.LocalCredentials.Add(new LocalCredential { UserId = userId, PasswordHash = hasher.HashPassword(user, request.NewPassword) });
        else { credential.PasswordHash = hasher.HashPassword(user, request.NewPassword); credential.FailedAttempts = 0; credential.LockedUntilUtc = null; }
        Audit(nameof(AppUser), userId, "PasswordResetByAdministrator", new { user.EmployeeId });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("divisions")]
    public async Task<IActionResult> CreateDivision(CreateDivisionRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var code = request.Code.Trim().ToUpperInvariant();
        // Treat an existing code as a normal validation outcome, before attempting a database insert.
        if (await db.Divisions.AsNoTracking().AnyAsync(x => x.Code == code, ct))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Division code already exists.",
                Detail = $"Division code '{code}' is already in use. Please enter a different code.",
                Extensions = { ["code"] = "division_code_exists", ["field"] = "code" }
            });
        }
        var division = new Division { Code = code, Name = request.Name.Trim() };
        db.Divisions.Add(division);
        Audit(nameof(Division), division.Id, "DivisionCreated", new { division.Code, division.Name });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/divisions/{division.Id}", new { division.Id });
    }

    private void Audit(string type, Guid id, string action, object data) => db.AuditEvents.Add(new AuditEvent
    { EntityType = type, EntityId = id, Action = action, ActorUserId = access.UserId, DataJson = JsonSerializer.Serialize(data) });
}
