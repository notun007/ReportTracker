using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/setup/reporting-frequencies")]
public sealed class ReportingFrequenciesController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    private static readonly HashSet<string> SupportedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "weekly", "twice-monthly", "monthly", "quarterly", "half-yearly", "yearly"
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportingFrequencyItem>>> List(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.HasAsync("administration.manage", null, null, ct) &&
            !await access.ConfigurationGrants().AnyAsync(ct))
            return Forbid();
        var frequencies = await db.ReportingFrequencies.AsNoTracking()
            .OrderBy(frequency => frequency.DisplayOrder)
            .ThenBy(frequency => frequency.Code)
            .Select(frequency => new ReportingFrequencyItem(frequency.Id, frequency.Code, frequency.Name, frequency.DisplayOrder))
            .ToListAsync(ct);
        return Ok(frequencies);
    }

    [HttpPost]
    public async Task<ActionResult<ReportingFrequencyItem>> Create(CreateReportingFrequencyRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var code = request.Code.Trim().ToLowerInvariant();
        var name = request.Name.Trim();
        if (!SupportedCodes.Contains(code))
            throw new TrackingValidationException("Choose one of the supported reporting frequency codes.");
        if (string.IsNullOrWhiteSpace(name))
            throw new TrackingValidationException("Enter a reporting frequency name.");
        if (await db.ReportingFrequencies.AnyAsync(x => x.Code.ToLower() == code, ct))
            return Conflict(new { field = "code", detail = "This reporting frequency already exists." });

        var frequency = new ReportingFrequency { Code = code, Name = name, DisplayOrder = request.DisplayOrder };
        db.ReportingFrequencies.Add(frequency);
        await db.SaveChangesAsync(ct);
        var item = new ReportingFrequencyItem(frequency.Id, frequency.Code, frequency.Name, frequency.DisplayOrder);
        return Created($"/api/setup/reporting-frequencies/{frequency.Id}", item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReportingFrequencyItem>> Update(int id, UpdateReportingFrequencyRequest request, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        var frequency = await db.ReportingFrequencies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (frequency is null) return NotFound();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new TrackingValidationException("Enter a reporting frequency name.");

        frequency.Name = name;
        frequency.DisplayOrder = request.DisplayOrder;
        await db.SaveChangesAsync(ct);
        return Ok(new ReportingFrequencyItem(frequency.Id, frequency.Code, frequency.Name, frequency.DisplayOrder));
    }
}
