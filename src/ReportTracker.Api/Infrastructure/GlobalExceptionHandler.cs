using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using Microsoft.Data.SqlClient;

namespace ReportTracker.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DbUpdateConcurrencyException or TrackingConflictException => StatusCodes.Status409Conflict,
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => StatusCodes.Status409Conflict,
            TrackingValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status == 500) logger.LogError(exception, "Unhandled API error. TraceId: {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status switch { 409 => "The record changed or conflicts with an existing record.", 400 => "Please check your input.", 403 => "Access denied.", _ => "An unexpected error occurred." },
            Detail = status == 500 ? null : exception is DbUpdateConcurrencyException ? "Refresh the record and try again." :
                exception is DbUpdateException ? "A record with this code, email, employee ID or period already exists." : exception.Message,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}
