using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/setup/submission-recipients")]
public sealed class SubmissionRecipientsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubmissionRecipientItem>>> List(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.Grants("reports.manage").AnyAsync(ct) &&
            !await access.HasAsync("administration.manage", null, null, ct)) return Forbid();

        return Ok(await db.SubmissionRecipients.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SubmissionRecipientItem(x.Id, x.Name,
                x.Departments.OrderBy(d => d.Name).Select(d => new LookupItem(d.Id, d.Name)).ToList()))
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSubmissionRecipientRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var name = ValidateName(request.Name, 250, "submission recipient");
        var normalized = name.ToUpperInvariant();
        if (await db.SubmissionRecipients.AnyAsync(x => x.Name.ToUpper() == normalized, ct))
            throw new TrackingConflictException("A submission recipient with that name already exists.");
        var recipient = new SubmissionRecipient { Name = name };
        db.SubmissionRecipients.Add(recipient);
        Audit(nameof(SubmissionRecipient), recipient.Id, "SubmissionRecipientCreated", new { recipient.Name });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new LookupItem(recipient.Id, recipient.Name));
    }

    [HttpPost("{recipientId:guid}/departments")]
    public async Task<IActionResult> CreateDepartment(Guid recipientId, CreateSubmissionDepartmentRequest request, CancellationToken ct)
    {
        var departments = await AddDepartments(recipientId, [request.Name], ct);
        return departments is null ? NotFound() : StatusCode(StatusCodes.Status201Created, departments[0]);
    }

    [HttpPost("{recipientId:guid}/departments/batch")]
    public async Task<IActionResult> CreateDepartments(Guid recipientId, CreateSubmissionDepartmentsRequest request, CancellationToken ct)
    {
        var departments = await AddDepartments(recipientId, request.Names, ct);
        return departments is null ? NotFound() : StatusCode(StatusCodes.Status201Created, departments);
    }

    private async Task<LookupItem[]?> AddDepartments(Guid recipientId, IReadOnlyList<string> names, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        if (!await db.SubmissionRecipients.AnyAsync(x => x.Id == recipientId, ct)) return null;
        if (names.Count is < 1 or > 20) throw new TrackingValidationException("Enter between 1 and 20 department names.");
        var clean = names.Select(name => ValidateName(name, 150, "department")).ToArray();
        var normalized = clean.Select(name => name.ToUpperInvariant()).ToArray();
        if (normalized.Distinct().Count() != normalized.Length)
            throw new TrackingValidationException("Enter distinct department names.");
        if (await db.SubmissionRecipientDepartments.AnyAsync(x => x.SubmissionRecipientId == recipientId && normalized.Contains(x.Name.ToUpper()), ct))
            throw new TrackingConflictException("A department with that name already exists for this recipient.");
        var departments = clean.Select(name => new SubmissionRecipientDepartment { SubmissionRecipientId = recipientId, Name = name }).ToArray();
        db.SubmissionRecipientDepartments.AddRange(departments);
        foreach (var department in departments)
            Audit(nameof(SubmissionRecipientDepartment), department.Id, "SubmissionRecipientDepartmentCreated", new { recipientId, department.Name });
        await db.SaveChangesAsync(ct);
        return departments.Select(department => new LookupItem(department.Id, department.Name)).ToArray();
    }

    private static string ValidateName(string name, int maxLength, string entity)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
            throw new TrackingValidationException($"Enter a {entity} name of at most {maxLength} characters.");
        return trimmed;
    }

    private void Audit(string type, Guid id, string action, object data) =>
        db.AuditEvents.Add(new AuditEvent { EntityType = type, EntityId = id, Action = action, ActorUserId = access.UserId, DataJson = JsonSerializer.Serialize(data) });
}
