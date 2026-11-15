using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/setup/reporting-frequencies")]
public sealed class ReportingFrequenciesController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportingFrequencyItem>>> List(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.HasAsync("administration.manage", null, null, ct) &&
            !await access.Grants("reports.manage").AnyAsync(ct))
            return Forbid();
        var frequencies = await db.ReportingFrequencies.AsNoTracking()
            .OrderBy(frequency => frequency.DisplayOrder)
            .ThenBy(frequency => frequency.Code)
            .Select(frequency => new ReportingFrequencyItem(frequency.Id, frequency.Code, frequency.Name, frequency.DisplayOrder))
            .ToListAsync(ct);
        return Ok(frequencies);
    }
}
