using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;

namespace ReportTracker.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(ITrackingService trackingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await trackingService.GetDashboardAsync(cancellationToken));
    }
}
