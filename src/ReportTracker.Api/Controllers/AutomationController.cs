using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Services;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/automation")]
public sealed class AutomationController(ICycleGenerationService generator, TrackingAccess access, ReportTrackerDbContext db) : ControllerBase
{
    [HttpPost("generate-cycles")]
    public async Task<ActionResult<GenerationResult>> GenerateCycles([FromQuery] DateOnly through, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        try
        {
            return Ok(await generator.GenerateAsync(through, ct));
        }
        catch (DbUpdateException exception) when (IsDuplicateCycle(exception))
        {
            return DuplicateCycleConflict();
        }
    }

    [HttpGet("yearly-cycles/preview")]
    public async Task<ActionResult<YearlyCyclePreview>> PreviewYear([FromQuery] int year, [FromQuery] Guid? reportId, CancellationToken ct)
    {
        await AuthorizeYearlyGeneration(reportId, ct);
        return Ok(await generator.PreviewYearAsync(year, reportId, ct));
    }

    [HttpPost("yearly-cycles")]
    public async Task<ActionResult<YearlyCycleGenerationResult>> GenerateYear([FromQuery] int year,
        GenerateYearlyCyclesRequest request, CancellationToken ct)
    {
        await AuthorizeYearlyGeneration(request.ReportId, ct);
        try
        {
            return Ok(await generator.GenerateYearAsync(year, request.ReportId, request.PeriodKeys, ct));
        }
        catch (DbUpdateException exception) when (IsDuplicateCycle(exception))
        {
            return DuplicateCycleConflict();
        }
    }

    private static bool IsDuplicateCycle(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "uq_rt_report_cycles_report_definition_id_period_key", StringComparison.OrdinalIgnoreCase) == true;

    private ConflictObjectResult DuplicateCycleConflict() => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Reporting cycles already exist.",
        Detail = "One or more selected reporting cycles already exist. Refresh the cycle list and generate only missing periods."
    });

    private async Task AuthorizeYearlyGeneration(Guid? reportId, CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (await access.HasAsync("administration.manage", null, null, ct)) return;
        if (!reportId.HasValue)
        {
            await access.RequireAsync("administration.manage", null, null, ct);
            return;
        }
        var report = await db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId.Value, ct)
            ?? throw new TrackingValidationException("Choose an active report.");
        await access.RequireAsync("reports.manage", report.OwnerDivisionId, report.Id, ct);
    }
}
