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
    private const string MainSenderResponsibility = "MainSender";
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.ConfigurationGrants().AnyAsync(ct) && !await access.HasAsync("administration.manage", null, null, ct)) return Forbid();
        return Ok(new
        {
            Divisions = await db.Divisions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new LookupItem(x.Id, x.Name)).ToListAsync(ct),
            Officers = await db.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayName).Select(x => new { x.Id, Name = x.DisplayName, x.DivisionId }).ToListAsync(ct),
            ReportTypes = await db.ReportTypes.AsNoTracking().OrderBy(x => x.Id).Select(x => new ReportTypeItem(x.Id, x.Name)).ToListAsync(ct)
        });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        var grants = access.ConfigurationGrants();
        var editors = access.Grants("reports.manage");
        var reports = await db.ReportDefinitions.AsNoTracking().Where(x => grants.Any(g =>
                (!g.DivisionId.HasValue || g.DivisionId == x.OwnerDivisionId) && (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.Id)))
            .OrderBy(x => x.Name).Select(x => new
            {
                x.Id, x.Code, x.Name, x.ReportTypeId, ReportTypeName = x.ReportTypeId == 1 ? "RIT" : "None RIT", x.OwnerDivisionId, x.SubmittingDivisionId, x.ReceivingDivisionId, x.BbRecipient,
                x.SubmissionRecipientId, x.InstructionReference, x.Description, x.RowVersion,
                CanEdit = x.IsActive && editors.Any(g => (!g.DivisionId.HasValue || g.DivisionId == x.OwnerDivisionId) &&
                    (!g.ReportDefinitionId.HasValue || g.ReportDefinitionId == x.Id)),
                RecipientName = x.SubmissionRecipient != null ? x.SubmissionRecipient.Name : x.BbRecipient,
                Departments = x.SubmissionDepartments.Select(d => new { d.DepartmentId, d.Department.Name }).ToList(),
                Version = x.Versions.OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.Id, v.Frequency, v.DueTime, v.ComponentDeadlineOffsetDays, v.TimeZoneId }).FirstOrDefault(),
                CycleCount = db.ReportCycles.Count(c => c.ReportDefinitionId == x.Id)
            }).ToListAsync(ct);
        var versionIds = reports.Where(x => x.Version is not null).Select(x => x.Version!.Id).ToArray();
        var deadlineRules = await db.ScheduleRules.AsNoTracking().Where(x => versionIds.Contains(x.DefinitionVersionId))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var deadlineOffsets = deadlineRules.GroupBy(x => x.DefinitionVersionId)
            .ToDictionary(x => x.Key, x => x.First().OffsetDays ?? 0);
        var ids = reports.Select(x => x.Id).ToArray();
        var assignments = await CurrentResponsibilities().AsNoTracking().Where(x => ids.Contains(x.ReportDefinitionId) &&
            (x.ResponsibilityType == MainSenderResponsibility ||
                x.ResponsibilityType == ReportResponsibilityAssignmentsController.ControllerResponsibility ||
                x.ResponsibilityType == TrackingAccess.ComponentReceiverResponsibility))
            .OrderByDescending(x => x.EffectiveFromUtc).ToListAsync(ct);
        var byRole = assignments.ToLookup(a => (a.ReportDefinitionId, (Guid?)a.DivisionId, a.ResponsibilityType));
        return Ok(reports.Select(x =>
        {
            var sender = byRole[(x.Id, x.SubmittingDivisionId, MainSenderResponsibility)].FirstOrDefault();
            var locks = new ReportDivisionLocks(
                byRole.Contains((x.Id, x.OwnerDivisionId, ReportResponsibilityAssignmentsController.ControllerResponsibility)),
                byRole.Contains((x.Id, x.ReceivingDivisionId, TrackingAccess.ComponentReceiverResponsibility)),
                sender is not null);
            return new ReportItem(x.Id, x.Code, x.Name, x.OwnerDivisionId, x.BbRecipient, (x.Version?.Frequency ?? default).ToString(), x.CycleCount,
                x.ReportTypeId, x.ReportTypeName,
                x.SubmittingDivisionId, sender?.PrimaryAssigneeId, sender?.AlternateAssigneeId, x.ReceivingDivisionId,
                x.SubmissionRecipientId, x.Departments.Select(d => d.DepartmentId).ToArray(),
                ReportDefinition.FormatSubmissionDestination(x.RecipientName, x.Departments.Select(d => d.Name)),
                x.Version?.DueTime, x.Version?.ComponentDeadlineOffsetDays, x.Version?.TimeZoneId,
                x.InstructionReference, x.Description,
                x.Version is null ? 0 : deadlineOffsets.GetValueOrDefault(x.Version.Id),
                Convert.ToBase64String(x.RowVersion), x.CanEdit, locks);
        }));
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport(CreateReportRequest request, CancellationToken ct)
    {
        await access.RequireAsync("reports.manage", request.OwnerDivisionId, null, ct);
        if (request.ReportTypeId is not (1 or 2))
            throw new TrackingValidationException("Choose a valid report type.");
        var recipient = request.SubmissionRecipientId is Guid recipientId
            ? await ValidateDestination(recipientId, request.SubmissionDepartmentIds, ct)
            : null;
        if (recipient is null && (string.IsNullOrWhiteSpace(request.BbRecipient) || request.SubmissionDepartmentIds is { Count: > 0 }))
            throw new TrackingValidationException("Choose a submission recipient and its departments.");
        await ValidateResponsibilities(new ReportResponsibilitiesRequest(request.OwnerDivisionId, request.SubmittingDivisionId ?? Guid.Empty,
            request.PrimarySubmittingAssigneeId ?? Guid.Empty, request.AlternateSubmittingAssigneeId), ct, allowUnassigned: true);
        if (!Enum.TryParse<Frequency>(request.Frequency, out var frequency) || !Enum.IsDefined(frequency)) throw new TrackingValidationException("Choose a valid reporting frequency.");
        if (request.ReportDeadlineOffsetDays is < 0 or > 365) throw new TrackingValidationException("Report deadline days must be between 0 and 365 calendar days after period end.");
        if (request.ComponentDeadlineOffsetDays is < 0 or > 365) throw new TrackingValidationException("Deadline lead days must be between 0 and 365 days.");
        if (!request.ReceivingDivisionId.HasValue || !await db.Divisions.AnyAsync(x => x.Id == request.ReceivingDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("Choose an active receiving division for the report's components.");
        var components = (request.Components ?? []).ToList();
        if (components.Count > 100) throw new TrackingValidationException("A report can contain at most 100 components.");
        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component is null) throw new TrackingValidationException("Every component must contain its configuration.");
            if (component.Contributors is { Count: > 0 })
            {
                var (primary, deputy) = await ComponentValidation.ValidateContributorsAsync(db, component.ProviderDivisionId, component.Contributors, ct);
                components[i] = component = component with { PrimaryAssigneeId = primary!.Value, AlternateAssigneeId = deputy };
            }
            await ComponentValidation.ValidateAsync(db, component.Name, component.Description, component.ProviderDivisionId,
                request.ReceivingDivisionId.Value, component.PrimaryAssigneeId, component.AlternateAssigneeId, request.ComponentDeadlineOffsetDays, ct, allowUnassigned: true);
        }
        if (components.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != components.Count)
            throw new TrackingValidationException("Component names must be unique within a report.");
        var report = new ReportDefinition { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), ReportTypeId = request.ReportTypeId, Description = request.Description?.Trim(),
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
        db.ScheduleRules.Add(new ScheduleRule
        {
            DefinitionVersionId = version.Id,
            PeriodConvention = "PeriodEndPlusCalendarDays",
            OffsetDays = request.ReportDeadlineOffsetDays,
            UsesBusinessDays = false,
            HolidayAdjustmentAuthorized = false
        });
        foreach (var item in components)
        {
            var component = new ComponentDefinition { DefinitionVersionId = version.Id, Name = item.Name.Trim(), Description = item.Description?.Trim(),
                ProviderDivisionId = item.ProviderDivisionId,
                PrimaryAssigneeId = item.PrimaryAssigneeId, ReceivingDivisionId = request.ReceivingDivisionId.Value,
                ContributorsJson = JsonSerializer.Serialize(item.Contributors ?? ComponentValidation.Contributors(null, item.PrimaryAssigneeId, item.AlternateAssigneeId)),
                DeadlineRuleJson = JsonSerializer.Serialize(new { offsetDays = request.ComponentDeadlineOffsetDays }) };
            db.ComponentDefinitions.Add(component);
            Audit(nameof(ComponentDefinition), component.Id, "ComponentCreated", new { reportId = report.Id, component.Name, component.ProviderDivisionId });
        }
        if (request.PrimarySubmittingAssigneeId is Guid primaryAssigneeId && primaryAssigneeId != Guid.Empty)
            db.ResponsibilityAssignments.Add(new ResponsibilityAssignment { ReportDefinitionId = report.Id, DivisionId = request.SubmittingDivisionId!.Value,
                PrimaryAssigneeId = primaryAssigneeId, AlternateAssigneeId = request.AlternateSubmittingAssigneeId,
                ResponsibilityType = "MainSender", EffectiveFromUtc = DateTime.UtcNow });
        Audit(nameof(ReportDefinition), report.Id, "ReportCreated", new { report.Code, report.Name, report.SubmissionRecipientId,
            request.SubmissionDepartmentIds, request.ReportDeadlineOffsetDays, request.ComponentDeadlineOffsetDays });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/reports/{report.Id}", new { report.Id });
    }

    [HttpPut("reports/{reportId:guid}/configuration")]
    public async Task<IActionResult> UpdateReportConfiguration(Guid reportId, UpdateReportConfigurationRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.Include(x => x.SubmissionDepartments)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, reportId, ct);
        if (request.OwnerDivisionId != report.OwnerDivisionId && request.OwnerDivisionId != Guid.Empty)
            await access.RequireAsync("reports.manage", request.OwnerDivisionId, reportId, ct);

        if (request.RowVersion != Convert.ToBase64String(report.RowVersion))
            throw new TrackingConflictException("This report changed since you opened it. Reopen Edit report and try again.");

        var version = await db.DefinitionVersions.Where(x => x.ReportDefinitionId == reportId)
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (version is null) throw new TrackingConflictException("This report has no definition version to update.");
        var schedule = await db.ScheduleRules.Where(x => x.DefinitionVersionId == version.Id)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? report.Name;
        var reportTypeId = request.ReportTypeId ?? report.ReportTypeId;
        var dueTime = request.DueTime ?? version.DueTime;
        var previousReportOffset = schedule?.OffsetDays ?? 0;
        var reportOffset = request.ReportDeadlineOffsetDays ?? previousReportOffset;
        var componentLead = request.ComponentDeadlineOffsetDays ?? version.ComponentDeadlineOffsetDays;
        var instruction = request.InstructionReference is null ? report.InstructionReference : TrimOptional(request.InstructionReference);
        var description = request.Description is null ? report.Description : TrimOptional(request.Description);

        if (request.Code is not null && request.Code != report.Code)
            errors["code"] = ["Report code cannot be changed after creation."];
        if (request.Name is not null && name.Length is 0 or > 150)
            errors["name"] = ["Enter a report name of at most 150 characters."];
        if (reportTypeId is not (1 or 2))
            errors["reportTypeId"] = ["Choose a valid report type."];
        if (reportOffset is < 0 or > 365)
            errors["reportDeadlineOffsetDays"] = ["Report deadline days must be between 0 and 365 calendar days after period end."];
        if (componentLead is < 0 or > 365)
            errors["componentDeadlineOffsetDays"] = ["Deadline lead days must be between 0 and 365 days."];
        if (request.InstructionReference?.Length > 250) errors["instructionReference"] = ["Use at most 250 characters."];
        if (request.Description?.Length > 1000) errors["description"] = ["Use at most 1,000 characters."];

        var previousDepartmentIds = report.SubmissionDepartments.Select(x => x.DepartmentId).Order().ToArray();
        var destinationSupplied = request.SubmissionRecipientId.HasValue || request.SubmissionDepartmentIds is not null;
        var destinationChanged = destinationSupplied &&
            (request.SubmissionRecipientId != report.SubmissionRecipientId ||
                !previousDepartmentIds.SequenceEqual((request.SubmissionDepartmentIds ?? []).Order()));
        SubmissionRecipient? recipient = null;
        if (destinationChanged)
        {
            try
            {
                recipient = await ValidateDestination(request.SubmissionRecipientId ?? Guid.Empty, request.SubmissionDepartmentIds, ct);
            }
            catch (TrackingValidationException exception)
            {
                errors["submissionDepartmentIds"] = [exception.Message];
            }
        }
        var validFrequency = Enum.TryParse<Frequency>(request.Frequency, out var frequency) &&
            Enum.GetName(frequency) == request.Frequency;
        if (!validFrequency)
            errors["frequency"] = ["Choose a valid reporting frequency."];
        else if (frequency != version.Frequency && await db.ReportCycles.AnyAsync(x => x.ReportDefinitionId == reportId, ct))
            errors["frequency"] = ["Frequency is locked because at least one reporting cycle exists."];

        async Task CheckDivision(string field, string label, Guid? current, Guid? proposed, string type, string responsibility)
        {
            if (current == proposed) return;
            if (current is Guid currentId && await HasDivisionResponsibility(reportId, currentId, type, ct))
                errors[field] = [$"{label} is locked because {responsibility} is assigned in the current division."];
            else if (!proposed.HasValue || proposed == Guid.Empty || !await db.Divisions.AnyAsync(x => x.Id == proposed && x.IsActive, ct))
                errors[field] = [$"Choose an active {label.ToLowerInvariant()}."];
            else if (await HasDivisionResponsibility(reportId, proposed.Value, type, ct))
                errors[field] = [$"{label} cannot change because the proposed division already has a {responsibility} responsibility assigned."];
        }

        await CheckDivision("ownerDivisionId", "Coordinating Division", report.OwnerDivisionId, request.OwnerDivisionId,
            ReportResponsibilityAssignmentsController.ControllerResponsibility, "Report Controller");
        await CheckDivision("receivingDivisionId", "Receiving Division", report.ReceivingDivisionId, request.ReceivingDivisionId,
            TrackingAccess.ComponentReceiverResponsibility, "Report Receiver");
        await CheckDivision("submittingDivisionId", "Final Submitting Division", report.SubmittingDivisionId, request.SubmittingDivisionId,
            MainSenderResponsibility, "Final Submission");
        if (errors.Count > 0) return BadRequest(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });

        var previous = new { report.Code, report.Name, report.ReportTypeId, version.Frequency, report.OwnerDivisionId,
            report.ReceivingDivisionId, report.SubmittingDivisionId, version.DueTime, ReportDeadlineOffsetDays = previousReportOffset,
            version.ComponentDeadlineOffsetDays, report.SubmissionRecipientId, SubmissionDepartmentIds = previousDepartmentIds,
            report.InstructionReference, report.Description };
        var current = new { report.Code, Name = name, ReportTypeId = reportTypeId, Frequency = frequency, request.OwnerDivisionId,
            request.ReceivingDivisionId, request.SubmittingDivisionId, DueTime = dueTime, ReportDeadlineOffsetDays = reportOffset,
            ComponentDeadlineOffsetDays = componentLead, SubmissionRecipientId = recipient?.Id ?? report.SubmissionRecipientId,
            SubmissionDepartmentIds = destinationChanged ? request.SubmissionDepartmentIds!.Order().ToArray() : previousDepartmentIds,
            InstructionReference = instruction, Description = description };
        if (previous.Equals(current))
            return NoContent();

        var receiverChanged = request.ReceivingDivisionId != report.ReceivingDivisionId;
        if (receiverChanged || componentLead != version.ComponentDeadlineOffsetDays)
        {
            var components = await db.ComponentDefinitions.Where(x => x.DefinitionVersionId == version.Id).ToListAsync(ct);
            foreach (var component in components)
            {
                if (receiverChanged) component.ReceivingDivisionId = request.ReceivingDivisionId!.Value;
                // Existing components with the old default follow the new default. Preserve legacy custom offsets.
                if (componentLead != version.ComponentDeadlineOffsetDays &&
                    UsesStoredComponentDefault(component.DeadlineRuleJson, version.ComponentDeadlineOffsetDays))
                    component.DeadlineRuleJson = JsonSerializer.Serialize(new { offsetDays = componentLead });
            }
        }
        if (reportOffset != previousReportOffset)
        {
            if (schedule is null)
            {
                schedule = new ScheduleRule { DefinitionVersionId = version.Id, PeriodConvention = "PeriodEndPlusCalendarDays" };
                db.ScheduleRules.Add(schedule);
            }
            schedule.PeriodConvention = "PeriodEndPlusCalendarDays";
            schedule.OffsetDays = reportOffset;
            schedule.FixedDay = null;
            schedule.UsesBusinessDays = false;
            schedule.HolidayAdjustmentAuthorized = false;
        }
        if (recipient is not null) ApplyDestination(report, recipient, request.SubmissionDepartmentIds!);
        version.Frequency = frequency;
        version.DueTime = dueTime;
        version.ComponentDeadlineOffsetDays = componentLead;
        report.Name = name;
        report.ReportTypeId = reportTypeId;
        report.Description = description;
        report.InstructionReference = instruction;
        report.OwnerDivisionId = request.OwnerDivisionId;
        report.ReceivingDivisionId = request.ReceivingDivisionId;
        report.SubmittingDivisionId = request.SubmittingDivisionId;
        report.UpdatedAtUtc = DateTime.UtcNow;
        Audit(nameof(ReportDefinition), reportId, "ReportConfigurationChanged", new { Previous = previous, Current = current });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? TrimOptional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool UsesStoredComponentDefault(string? deadlineRuleJson, int oldDefault)
    {
        if (string.IsNullOrWhiteSpace(deadlineRuleJson)) return false;
        using var rule = JsonDocument.Parse(deadlineRuleJson);
        return rule.RootElement.ValueKind == JsonValueKind.Object &&
            rule.RootElement.TryGetProperty("offsetDays", out var offset) &&
            offset.ValueKind == JsonValueKind.Number && offset.TryGetInt32(out var days) && days == oldDefault;
    }

    private IQueryable<ResponsibilityAssignment> CurrentResponsibilities()
    {
        var now = DateTime.UtcNow;
        return db.ResponsibilityAssignments.Where(x => x.EffectiveFromUtc <= now &&
            (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now));
    }

    private Task<bool> HasDivisionResponsibility(Guid reportId, Guid divisionId, string type, CancellationToken ct) =>
        CurrentResponsibilities().AnyAsync(x => x.ReportDefinitionId == reportId && x.DivisionId == divisionId &&
            x.ResponsibilityType == type, ct);

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
        ApplyDestination(report, recipient, request.SubmissionDepartmentIds);
        report.UpdatedAtUtc = DateTime.UtcNow;
        Audit(nameof(ReportDefinition), report.Id, "SubmissionDestinationChanged", new
        {
            previousRecipientId, previousDepartmentIds, report.SubmissionRecipientId, request.SubmissionDepartmentIds
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void ApplyDestination(ReportDefinition report, SubmissionRecipient recipient, IReadOnlyList<Guid> departmentIds)
    {
        report.SubmissionRecipientId = recipient.Id;
        report.BbRecipient = recipient.Name;
        var selected = departmentIds.ToHashSet();
        var existing = report.SubmissionDepartments.Select(x => x.DepartmentId).ToHashSet();
        db.ReportSubmissionDepartments.RemoveRange(report.SubmissionDepartments.Where(x => !selected.Contains(x.DepartmentId)).ToArray());
        foreach (var departmentId in selected.Except(existing))
            report.SubmissionDepartments.Add(new ReportSubmissionDepartment { ReportDefinitionId = report.Id, DepartmentId = departmentId });
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
        await access.RequireAsync(AccessPermissions.Responsibilities, report.OwnerDivisionId, reportId, ct);
        if (request.CoordinatingDivisionId != report.OwnerDivisionId)
            await access.RequireAsync(AccessPermissions.Responsibilities, request.CoordinatingDivisionId, reportId, ct);
        await ValidateResponsibilities(request, ct);
        if (request.CoordinatingDivisionId != report.OwnerDivisionId && await HasDivisionResponsibility(reportId,
            request.CoordinatingDivisionId, ReportResponsibilityAssignmentsController.ControllerResponsibility, ct))
            throw new TrackingValidationException("Coordinating Division cannot change because the proposed division already has a Report Controller responsibility assigned.");
        if (request.SubmittingDivisionId != report.SubmittingDivisionId &&
            await HasDivisionResponsibility(reportId, request.SubmittingDivisionId, MainSenderResponsibility, ct))
            throw new TrackingValidationException("Final Submitting Division cannot change because the proposed division already has a Final Submission responsibility assigned.");
        var now = DateTime.UtcNow;
        var assignments = await db.ResponsibilityAssignments.Where(x => x.ReportDefinitionId == reportId && x.ResponsibilityType == "MainSender" &&
            (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > now)).ToListAsync(ct);
        foreach (var assignment in assignments) assignment.EffectiveToUtc = now;
        var previousCoordinating = report.OwnerDivisionId; var previousSubmitting = report.SubmittingDivisionId;
        report.OwnerDivisionId = request.CoordinatingDivisionId; report.SubmittingDivisionId = request.SubmittingDivisionId;
        report.UpdatedAtUtc = now;
        db.ResponsibilityAssignments.Add(new ResponsibilityAssignment { ReportDefinitionId = reportId, DivisionId = request.SubmittingDivisionId,
            PrimaryAssigneeId = request.PrimarySubmittingAssigneeId, AlternateAssigneeId = request.AlternateSubmittingAssigneeId,
            ResponsibilityType = "MainSender", EffectiveFromUtc = now });
        Audit(nameof(ReportDefinition), reportId, "ReportResponsibilitiesChanged", new { previousCoordinating, previousSubmitting, request });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ValidateResponsibilities(ReportResponsibilitiesRequest request, CancellationToken ct, bool allowUnassigned = false)
    {
        if (!await db.Divisions.AnyAsync(x => x.Id == request.CoordinatingDivisionId && x.IsActive, ct) ||
            !await db.Divisions.AnyAsync(x => x.Id == request.SubmittingDivisionId && x.IsActive, ct))
            throw new TrackingValidationException("Choose active coordinating and final submitting divisions.");
        if (allowUnassigned && request.PrimarySubmittingAssigneeId == Guid.Empty)
        {
            if (request.AlternateSubmittingAssigneeId.HasValue)
                throw new TrackingValidationException("Choose a primary submitting assignee before assigning an alternate assignee.");
            return;
        }
        if (!await db.Users.AnyAsync(x => x.Id == request.PrimarySubmittingAssigneeId && x.DivisionId == request.SubmittingDivisionId && x.IsActive, ct) ||
            (request.AlternateSubmittingAssigneeId.HasValue && !await db.Users.AnyAsync(x => x.Id == request.AlternateSubmittingAssigneeId && x.DivisionId == request.SubmittingDivisionId && x.IsActive, ct)))
            throw new TrackingValidationException("Final submitting assignees must be active members of the final submitting division.");
        if (request.AlternateSubmittingAssigneeId == request.PrimarySubmittingAssigneeId)
            throw new TrackingValidationException("Choose a different alternate submitting assignee.");
    }

    [HttpGet("reports/{reportId:guid}/cycles")]
    public async Task<ActionResult<IReadOnlyList<ReportingCycleItem>>> Cycles(Guid reportId, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Cycles, report.OwnerDivisionId, reportId, ct);
        var cycles = await db.ReportCycles.AsNoTracking().Where(x => x.ReportDefinitionId == reportId)
            .Include(x => x.Obligations).OrderByDescending(x => x.PeriodStart).ThenBy(x => x.PeriodKey).ToListAsync(ct);
        var obligations = cycles.SelectMany(x => x.Obligations).ToList();
        var officerIds = obligations.SelectMany(x => new[] { x.PrimaryAssigneeId, x.AlternateAssigneeId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var divisionIds = obligations.SelectMany(x => new Guid?[] { x.AccountableDivisionId, x.ReceivingDivisionId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var officers = await db.Users.AsNoTracking().Where(x => officerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var divisions = await db.Divisions.AsNoTracking().Where(x => divisionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Ok(cycles.Select(cycle => new ReportingCycleItem(cycle.Id, cycle.PeriodKey, cycle.PeriodLabel, cycle.PeriodStart, cycle.PeriodEnd,
            cycle.Obligations.OrderBy(x => x.Type == ObligationType.External ? 0 : 1).ThenBy(x => x.Name).Select(x =>
                new CycleObligationItem(TrackingRules.ObligationDisplayName(x.Type, x.Name), x.Type == ObligationType.External ? "BB" : x.Type.ToString(),
                    x.AccountableDivisionId, divisions.GetValueOrDefault(x.AccountableDivisionId, "Unknown division"),
                    x.PrimaryAssigneeId, officers.GetValueOrDefault(x.PrimaryAssigneeId ?? Guid.Empty),
                    x.AlternateAssigneeId, officers.GetValueOrDefault(x.AlternateAssigneeId ?? Guid.Empty), DisplayRecipient(x, divisions),
                    x.EffectiveDueAtUtc, x.ReceiptStatus != ReceiptStatus.NotRequired, x.EvidenceStatus != EvidenceStatus.NotRequired, x.ReceivingDivisionId,
                    x.Type == ObligationType.Internal ? ComponentValidation.Contributors(x.ContributorsJson, x.PrimaryAssigneeId, x.AlternateAssigneeId) : null)).ToList())).ToList());
    }

    [HttpPost("reports/{reportId:guid}/cycles")]
    public async Task<IActionResult> CreateCycle(Guid reportId, CreateCycleRequest request, CancellationToken ct)
    {
        var report = await db.ReportDefinitions.Include(x => x.Versions).Include(x => x.SubmissionRecipient)
            .Include(x => x.SubmissionDepartments).ThenInclude(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, ct);
        if (report is null) return NotFound();
        await access.RequireAsync(AccessPermissions.Cycles, report.OwnerDivisionId, reportId, ct);
        if (request.PeriodStart == default || request.PeriodEnd < request.PeriodStart) throw new TrackingValidationException("Enter a valid reporting period.");
        var reportingYear = request.ReportingYear ?? request.PeriodStart.Year;
        if (reportingYear is < 2000 or > 9998 || request.PeriodStart.Year != reportingYear || request.PeriodEnd.Year != reportingYear)
            throw new TrackingValidationException("Period start and period end must both be within the selected reporting year.");
        var periodKey = request.PeriodKey.Trim();
        if (string.IsNullOrWhiteSpace(periodKey)) throw new TrackingValidationException("Enter a reporting period key.");
        var conflicts = await db.ReportCycles.AsNoTracking().Where(x => x.ReportDefinitionId == reportId &&
                (x.PeriodKey == periodKey || x.PeriodStart == request.PeriodStart || x.PeriodEnd == request.PeriodEnd ||
                 (request.PeriodStart <= x.PeriodEnd && request.PeriodEnd >= x.PeriodStart)))
            .Select(x => new { x.PeriodKey, x.PeriodLabel, x.PeriodStart, x.PeriodEnd }).ToListAsync(ct);
        if (conflicts.Any(x => x.PeriodKey == periodKey))
            throw new TrackingConflictException("This reporting cycle already exists for the selected report and period.");
        if (conflicts.Any(x => x.PeriodStart == request.PeriodStart))
            throw new TrackingConflictException("A reporting cycle for this report already starts on the selected date.");
        if (conflicts.Any(x => x.PeriodEnd == request.PeriodEnd))
            throw new TrackingConflictException("A reporting cycle for this report already ends on the selected date.");
        if (conflicts.FirstOrDefault() is { } overlap)
            throw new TrackingConflictException($"The selected reporting period overlaps {overlap.PeriodLabel} ({overlap.PeriodStart:yyyy-MM-dd} to {overlap.PeriodEnd:yyyy-MM-dd}).");
        if (!request.Obligations.Any(x => x.Type == "BB")) throw new TrackingValidationException("Include at least one final submission obligation.");
        var version = report.Versions.OrderByDescending(x => x.VersionNumber).First();
        var cycle = new ReportCycle { ReportDefinitionId = reportId, DefinitionVersionId = version.Id, PeriodKey = periodKey,
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
                !await db.Users.AnyAsync(x => x.Id == item.PrimaryAssigneeId && x.DivisionId == item.DivisionId && x.IsActive, ct) ||
                (item.AlternateAssigneeId.HasValue && !await db.Users.AnyAsync(x => x.Id == item.AlternateAssigneeId && x.DivisionId == item.DivisionId && x.IsActive, ct)))
                throw new TrackingValidationException("Primary and alternate assignees must be active members of the accountable division.");
            var obligation = new TrackingObligation { ReportCycleId = cycle.Id, Type = item.Type == "BB" ? ObligationType.External : ObligationType.Internal,
                Name = item.Name.Trim(), AccountableDivisionId = item.DivisionId, PrimaryAssigneeId = item.PrimaryAssigneeId, AlternateAssigneeId = item.AlternateAssigneeId,
                Recipient = item.Type == "BB" && report.SubmissionRecipientId.HasValue ? report.SubmissionDestination() : item.Recipient.Trim(),
                ReceivingDivisionId = receivingDivision, OriginalDueAtUtc = item.DueAtUtc, EffectiveDueAtUtc = item.DueAtUtc,
                ReceiptStatus = item.ReceiptRequired ? ReceiptStatus.AwaitingConfirmation : ReceiptStatus.NotRequired,
                EvidenceStatus = item.EvidenceRequired ? EvidenceStatus.Missing : EvidenceStatus.NotRequired };
            obligations.Add(obligation);
            cycle.Obligations.Add(obligation);
        }
        var main = obligations.Single(x => x.Type == ObligationType.External);
        foreach (var component in obligations.Where(x => x.Type == ObligationType.Internal)) component.ParentObligationId = main.Id;
        report.UpdatedAtUtc = DateTime.UtcNow;
        db.ReportCycles.Add(cycle);
        Audit(nameof(ReportCycle), cycle.Id, "CycleCreated", new { reportId, cycle.PeriodKey, ObligationCount = cycle.Obligations.Count });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (CycleUniqueConstraint(exception))
        {
            return Conflict(CycleConflictProblem(exception));
        }
        return Created($"/api/setup/reports/{reportId}/cycles/{cycle.Id}", new { cycle.Id });
    }

    private static bool CycleUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("uq_rt_report_cycles_report_definition_id_period_", StringComparison.OrdinalIgnoreCase) == true;

    private static string DisplayRecipient(TrackingObligation obligation, IReadOnlyDictionary<Guid, string> divisions)
    {
        if (obligation.Type != ObligationType.Internal) return obligation.Recipient;
        var divisionId = obligation.ReceivingDivisionId;
        if (!divisionId.HasValue && Guid.TryParse(obligation.Recipient, out var legacyDivisionId)) divisionId = legacyDivisionId;
        return divisionId.HasValue ? divisions.GetValueOrDefault(divisionId.Value, obligation.Recipient) : obligation.Recipient;
    }

    private static ProblemDetails CycleConflictProblem(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? string.Empty;
        var detail = message.Contains("period_start", StringComparison.OrdinalIgnoreCase)
            ? "A reporting cycle for this report already starts on the selected date."
            : message.Contains("period_end", StringComparison.OrdinalIgnoreCase)
                ? "A reporting cycle for this report already ends on the selected date."
                : "This reporting cycle already exists for the selected report and period.";
        return new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "The reporting cycle conflicts with an existing cycle.", Detail = detail };
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.HasAsync(AccessPermissions.Users,null,null,ct) && !await access.HasAsync(AccessPermissions.Roles,null,null,ct)) return Forbid();
        var users = await db.Users.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(ct);
        var roles = await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id
                           where assignment.IsActive && role.IsActive
                           select new { assignment.UserId, role.Name }).ToListAsync(ct);
        return Ok(users.Select(x => new UserItem(x.Id, x.EmployeeId, x.DisplayName, x.Email, x.MobileNumber, x.DivisionId, x.IsActive,
            roles.Where(r => r.UserId == x.Id).Select(r => r.Name).Distinct().ToArray(), Convert.ToBase64String(x.RowVersion))));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Users, null, null, ct);
        if (!await db.Divisions.AnyAsync(x => x.Id == request.DivisionId && x.IsActive, ct)) throw new TrackingValidationException("Choose an active division.");
        var employeeId = request.EmployeeId.Trim();
        var normalizedEmployeeId = employeeId.ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.EmployeeId.ToUpper() == normalizedEmployeeId, ct))
            return DuplicateUser("employeeId", "An account with this employee ID already exists.");
        if (await db.Users.AnyAsync(x => x.Email.ToLower() == email, ct))
            return DuplicateUser("email", "An account with this email address already exists.");
        var user = new AppUser { EmployeeId = employeeId, DisplayName = request.DisplayName.Trim(),
            Email = email, MobileNumber = request.MobileNumber?.Trim(), DivisionId = request.DivisionId };
        db.Users.Add(user);
        db.LocalCredentials.Add(new LocalCredential { UserId = user.Id, PasswordHash = hasher.HashPassword(user, request.Password) });
        var assignments = request.RoleAssignments?.ToList() ?? [];
        if (request.RoleAssignments is null && request.IsCoordinator)
        {
            var role = await db.Roles.SingleAsync(x => x.Code == "REPORT_ADMIN", ct);
            assignments.Add(new(role.Id,"Division",request.DivisionId));
        }
        if (assignments.Count > 0)
        {
            await access.RequireAsync(AccessPermissions.Roles,null,null,ct);
            var administration=new AccessAdministration(db,access);
            await administration.ValidateAssignmentsAsync(assignments,ct);
            await administration.ReplaceAssignmentsAsync(user,assignments,ct);
        }
        Audit(nameof(AppUser), user.Id, "UserCreated", new { user.EmployeeId, user.DivisionId, request.IsCoordinator });
        await db.SaveChangesAsync(ct);
        return Created($"/api/setup/users/{user.Id}", new { user.Id });
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> UpdateUser(Guid userId,UpdateAccessUserRequest request,CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Users,null,null,ct);
        var user=await db.Users.SingleOrDefaultAsync(x => x.Id == userId,ct); if (user is null) return NotFound();
        AccessAdministration.CheckVersion(user,request.RowVersion);
        var name=request.DisplayName.Trim(); var email=request.Email.Trim().ToLowerInvariant();
        if (name.Length == 0) throw new TrackingValidationException("Enter a display name.");
        if (!await db.Divisions.AnyAsync(x => x.Id == request.DivisionId && x.IsActive,ct)) throw new TrackingValidationException("Choose an active division.");
        if (await db.Users.AnyAsync(x => x.Id != userId && x.Email.ToLower() == email,ct)) return DuplicateUser("email","An account with this email address already exists.");
        var previous=new { user.DisplayName,user.Email,user.MobileNumber,user.DivisionId };
        user.DisplayName=name; user.Email=email; user.MobileNumber=request.MobileNumber?.Trim(); user.DivisionId=request.DivisionId;
        Audit(nameof(AppUser),userId,"UserProfileChanged",new { Previous=previous,user.DisplayName,user.Email,user.MobileNumber,user.DivisionId });
        await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpPatch("users/{userId:guid}/status")]
    public async Task<IActionResult> ChangeUserStatus(Guid userId, ChangeUserStatusRequest request, CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Users, null, null, ct);
        var administration=new AccessAdministration(db,access);
        await using var transaction=await administration.BeginMutationAsync(ct);
        await access.RequireAsync(AccessPermissions.Users,null,null,ct);
        if (userId == access.UserId && !request.IsActive) throw new TrackingConflictException("You cannot deactivate your own active session.");
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        if (!request.IsActive) await administration.EnsureAdministratorRemainsAsync(ct,deactivatedUserId:userId);
        user.IsActive = request.IsActive;
        Audit(nameof(AppUser), userId, request.IsActive ? "UserActivated" : "UserDeactivated", new { user.EmployeeId });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, ResetPasswordRequest request, CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Users, null, null, ct);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        var credential = await db.LocalCredentials.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential is null) db.LocalCredentials.Add(new LocalCredential { UserId = userId, PasswordHash = hasher.HashPassword(user, request.NewPassword) });
        else { credential.PasswordHash = hasher.HashPassword(user, request.NewPassword); credential.FailedAttempts = 0; credential.LockedUntilUtc = null; }
        Audit(nameof(AppUser), userId, "PasswordResetByAdministrator", new { user.EmployeeId });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("divisions")]
    public async Task<ActionResult<IReadOnlyList<DivisionItem>>> Divisions(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.HasAsync(AccessPermissions.Divisions,null,null,ct) && !await access.HasAsync(AccessPermissions.Users,null,null,ct) && !await access.HasAsync(AccessPermissions.Roles,null,null,ct)) return Forbid();
        return Ok(await db.Divisions.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new DivisionItem(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct));
    }

    [HttpPost("divisions")]
    public async Task<IActionResult> CreateDivision(CreateDivisionRequest request, CancellationToken ct)
    {
        await access.RequireAsync(AccessPermissions.Divisions, null, null, ct);
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

    private ConflictObjectResult DuplicateUser(string field, string detail) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "User already exists.",
        Detail = detail,
        Extensions = { ["field"] = field }
    });
}
