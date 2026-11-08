using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/automation")]
public sealed class AutomationController(ICycleGenerationService generator, TrackingAccess access) : ControllerBase
{
    [HttpPost("generate-cycles")]
    public async Task<ActionResult<GenerationResult>> GenerateCycles([FromQuery] DateOnly through, CancellationToken ct)
    {
        await access.RequireAsync("administration.manage", null, null, ct);
        return Ok(await generator.GenerateAsync(through, ct));
    }
}
