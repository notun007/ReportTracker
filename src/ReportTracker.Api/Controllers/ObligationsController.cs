using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;

namespace ReportTracker.Api.Controllers;

[ApiController]
[Route("api/obligations")]
[Authorize]
public sealed class ObligationsController(ITrackingService trackingService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ObligationListItemDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var obligation = await trackingService.GetObligationAsync(id, cancellationToken);
        return obligation is null ? NotFound() : Ok(obligation);
    }

    [HttpPost("{id:guid}/sending-events")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordSending(Guid id, [FromBody] RecordSendingCommand command, CancellationToken cancellationToken)
    {
        return await trackingService.RecordSendingAsync(id, command, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SendingHistoryDto>>> History(Guid id, CancellationToken cancellationToken)
    {
        var history = await trackingService.GetHistoryAsync(id, cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }
}
