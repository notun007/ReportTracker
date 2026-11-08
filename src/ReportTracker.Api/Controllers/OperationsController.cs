using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/obligations/{obligationId:guid}")]
public sealed class ObligationOperationsController(ReportTrackerDbContext db, TrackingAccess access,
    IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF])
    };

    [HttpGet("evidence")]
    public async Task<ActionResult<IReadOnlyList<EvidenceItem>>> Evidence(Guid obligationId, CancellationToken ct)
    {
        if (!await access.AuthorizedObligations().AnyAsync(x => x.Id == obligationId, ct)) return NotFound();
        var rows = await EvidenceQuery(obligationId).ToListAsync(ct);
        return Ok(rows.Select(NormalizeEvidence));
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<EvidenceItem>> UploadEvidence(Guid obligationId, IFormFile file, CancellationToken ct)
    {
        var obligation = await access.AuthorizedObligations(true).Include(x => x.SendingEvents).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return NotFound();
        if (file.Length is <= 0 or > 10_485_760) throw new TrackingValidationException("Evidence must be between 1 byte and 10 MB.");
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFiles.TryGetValue(extension, out var allowed)) throw new TrackingValidationException("Evidence must be PDF, PNG, JPG or JPEG.");
        await using var input = file.OpenReadStream();
        var prefix = new byte[allowed.Signature.Length];
        if (await input.ReadAsync(prefix, ct) != prefix.Length || !prefix.SequenceEqual(allowed.Signature))
            throw new TrackingValidationException("The uploaded file content does not match its extension.");
        input.Position = 0;
        var storageRoot = Path.GetFullPath(configuration["EvidenceStorage:Root"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence"));
        Directory.CreateDirectory(storageRoot);
        var storageKey = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var target = Path.GetFullPath(Path.Combine(storageRoot, storageKey));
        if (Path.GetRelativePath(storageRoot, target).StartsWith("..", StringComparison.Ordinal)) throw new TrackingValidationException("Invalid evidence path.");
        await using (var output = System.IO.File.Create(target)) await input.CopyToAsync(output, ct);
        await using var hashStream = System.IO.File.OpenRead(target);
        var evidence = new EvidenceDocument
        {
            TrackingObligationId = obligationId,
            SendingEventId = obligation.SendingEvents.Where(x => !x.IsReversed).OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefault(),
            Purpose = EvidencePurpose.SendingConfirmation, OriginalFileName = Path.GetFileName(file.FileName), StorageKey = storageKey,
            ContentType = allowed.ContentType, SizeBytes = file.Length, Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, ct)),
            Status = EvidenceStatus.Provided, UploadedByUserId = access.UserId
        };
        db.EvidenceDocuments.Add(evidence);
        obligation.EvidenceStatus = EvidenceStatus.Provided;
        Audit(nameof(EvidenceDocument), evidence.Id, "EvidenceUploaded", new { obligationId, evidence.OriginalFileName, evidence.SizeBytes, evidence.Sha256 });
        await db.SaveChangesAsync(ct);
        return Created($"/api/evidence/{evidence.Id}", NormalizeEvidence(await EvidenceQuery(obligationId).SingleAsync(x => x.Id == evidence.Id, ct)));
    }

    [HttpPost("receipts")]
    public async Task<ActionResult<ReceiptItem>> ConfirmReceipt(Guid obligationId, ReceiptRequest request, CancellationToken ct)
    {
        var obligation = await access.AuthorizedObligations(true).Include(x => x.SendingEvents).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return NotFound();
        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc || request.ReceivedAtUtc > DateTime.UtcNow.AddMinutes(5))
            throw new TrackingValidationException("Enter a valid receipt time in UTC.");
        if (request.IsDisputed && string.IsNullOrWhiteSpace(request.Reason)) throw new TrackingValidationException("A dispute reason is required.");
        var sending = obligation.SendingEvents.Where(x => !x.IsReversed).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault()
            ?? throw new TrackingConflictException("Record a submission before confirming receipt.");
        var receipt = new ReceiptConfirmation { SendingEventId = sending.Id, ActingUserId = access.UserId,
            ReceivedAtUtc = request.ReceivedAtUtc, IsDisputed = request.IsDisputed, Reason = request.Reason?.Trim() };
        db.ReceiptConfirmations.Add(receipt);
        obligation.ReceiptStatus = request.IsDisputed ? ReceiptStatus.ReceiptDisputed : ReceiptStatus.ReceiptConfirmed;
        Audit(nameof(ReceiptConfirmation), receipt.Id, request.IsDisputed ? "ReceiptDisputed" : "ReceiptConfirmed", new { obligationId, request.ReceivedAtUtc, request.Reason });
        await db.SaveChangesAsync(ct);
        var actor = await db.Users.Where(x => x.Id == access.UserId).Select(x => x.DisplayName).SingleAsync(ct);
        return Created($"/api/obligations/{obligationId}/receipts/{receipt.Id}", new ReceiptItem(receipt.Id, sending.Id,
            receipt.ReceivedAtUtc, receipt.IsDisputed, receipt.Reason, receipt.CreatedAtUtc, actor));
    }

    [HttpPost("extensions")]
    public async Task<ActionResult> RequestExtension(Guid obligationId, CreateExtensionRequest request, CancellationToken ct)
    {
        var obligation = await access.AuthorizedObligations(true).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return NotFound();
        if (request.RequestedDueAtUtc.Kind != DateTimeKind.Utc || request.RequestedDueAtUtc <= obligation.EffectiveDueAtUtc)
            throw new TrackingValidationException("The requested deadline must be a later UTC date and time.");
        if (await db.ExtensionRequests.AnyAsync(x => x.TrackingObligationId == obligationId && x.Status == ExtensionStatus.Pending, ct))
            throw new TrackingConflictException("A deadline extension is already pending.");
        var extension = new ExtensionRequest { TrackingObligationId = obligationId, OriginalDueAtUtc = obligation.EffectiveDueAtUtc,
            RequestedDueAtUtc = request.RequestedDueAtUtc, Reason = request.Reason.Trim(), PermissionReference = request.PermissionReference?.Trim(),
            RequestedByUserId = access.UserId, ApproverUserId = Guid.Empty };
        db.ExtensionRequests.Add(extension);
        Audit(nameof(ExtensionRequest), extension.Id, "ExtensionRequested", new { obligationId, extension.OriginalDueAtUtc, extension.RequestedDueAtUtc });
        await db.SaveChangesAsync(ct);
        return Created($"/api/extensions/{extension.Id}", new { extension.Id });
    }

    [HttpPost("amendments")]
    public async Task<ActionResult> AmendStatus(Guid obligationId, AmendmentRequest request, CancellationToken ct)
    {
        var obligation = await access.AuthorizedObligations(true).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return NotFound();
        if (!Enum.TryParse<SendingStatus>(request.ReplacementStatus.Replace(" ", string.Empty), true, out var status) || !Enum.IsDefined(status))
            throw new TrackingValidationException("Choose Not Sent, Partially Sent or Sent.");
        var previous = obligation.SendingStatus;
        if (previous == status) throw new TrackingConflictException("The replacement status is unchanged.");
        var amendment = new StatusAmendment { TrackingObligationId = obligationId, PreviousStatus = previous.ToString(),
            ReplacementStatus = status.ToString(), Reason = request.Reason.Trim(), ActingUserId = access.UserId };
        obligation.SendingStatus = status;
        db.StatusAmendments.Add(amendment);
        Audit(nameof(StatusAmendment), amendment.Id, "StatusAmended", new { obligationId, previous, status, amendment.Reason });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<EvidenceItem> EvidenceQuery(Guid obligationId) =>
        from evidence in db.EvidenceDocuments.AsNoTracking()
        join user in db.Users on evidence.UploadedByUserId equals user.Id
        where evidence.TrackingObligationId == obligationId
        orderby evidence.CreatedAtUtc descending
        select new EvidenceItem(evidence.Id, obligationId, evidence.OriginalFileName, evidence.ContentType, evidence.SizeBytes,
            evidence.Sha256, evidence.Status.ToString(), evidence.Purpose.ToString(), evidence.CreatedAtUtc, user.DisplayName);

    private static EvidenceItem NormalizeEvidence(EvidenceItem item) => item with
    { UploadedAtUtc = DateTime.SpecifyKind(item.UploadedAtUtc, DateTimeKind.Utc) };

    private void Audit(string type, Guid id, string action, object data) => db.AuditEvents.Add(new AuditEvent
    { EntityType = type, EntityId = id, Action = action, ActorUserId = access.UserId, DataJson = JsonSerializer.Serialize(data) });
}

[ApiController, Authorize, Route("api/evidence")]
public sealed class EvidenceController(ReportTrackerDbContext db, TrackingAccess access, IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var evidence = await db.EvidenceDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (evidence?.TrackingObligationId is not Guid obligationId || !await access.AuthorizedObligations().AnyAsync(x => x.Id == obligationId, ct)) return NotFound();
        var root = Path.GetFullPath(configuration["EvidenceStorage:Root"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence"));
        var target = Path.GetFullPath(Path.Combine(root, evidence.StorageKey));
        if (Path.GetRelativePath(root, target).StartsWith("..", StringComparison.Ordinal) || !System.IO.File.Exists(target)) return NotFound();
        return PhysicalFile(target, evidence.ContentType, evidence.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, EvidenceDecisionRequest request, CancellationToken ct)
    {
        var evidence = await db.EvidenceDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (evidence?.TrackingObligationId is not Guid obligationId) return NotFound();
        var obligation = await db.TrackingObligations.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition).FirstOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return NotFound();
        await access.RequireAsync("reports.manage", obligation.ReportCycle.ReportDefinition.OwnerDivisionId, obligation.ReportCycle.ReportDefinitionId, ct);
        if (!Enum.TryParse<EvidenceStatus>(request.Decision, true, out var decision) || decision is not (EvidenceStatus.Verified or EvidenceStatus.Rejected))
            throw new TrackingValidationException("Choose Verified or Rejected.");
        evidence.Status = decision;
        obligation.EvidenceStatus = decision;
        db.EvidenceDecisions.Add(new EvidenceDecision { EvidenceDocumentId = id, DecidedByUserId = access.UserId,
            Decision = decision, Reason = request.Reason?.Trim(), DecidedAtUtc = DateTime.UtcNow });
        db.AuditEvents.Add(new AuditEvent { EntityType = nameof(EvidenceDocument), EntityId = id, ActorUserId = access.UserId,
            Action = "Evidence" + decision, DataJson = JsonSerializer.Serialize(new { obligationId, request.Reason }) });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
