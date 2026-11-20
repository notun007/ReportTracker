using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ReportTracker.Application.Contracts;

namespace ReportTracker.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            await RequestCancellationResponse.HandleAsync(context, logger);
            return true;
        }
        if (DatabaseConnectionFailure.IsUnavailable(exception))
        {
            logger.LogWarning(exception, "Database connection unavailable. TraceId: {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            DatabaseConnectionFailure.SetHeaders(context.Response);
            await context.Response.WriteAsJsonAsync(DatabaseConnectionFailure.Problem(context),
                options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            return true;
        }
        var status = exception switch
        {
            DbUpdateConcurrencyException or TrackingConflictException => StatusCodes.Status409Conflict,
            DbUpdateException { InnerException: OracleException { Number: 1 } } => StatusCodes.Status409Conflict,
            DbUpdateException { InnerException: OracleException { Number: 8177 } } => StatusCodes.Status409Conflict,
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
            Detail = ProblemDetail(exception, status),
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }

    private static string? ProblemDetail(Exception exception, int status)
    {
        if (status == 500) return null;
        if (exception is DbUpdateConcurrencyException) return "Refresh the record and try again.";
        if (exception is DbUpdateException { InnerException: OracleException { Number: 1 } oracle })
        {
            if (oracle.Message.Contains("uq_rt_report_cycles_report_definition_id_period_start", StringComparison.OrdinalIgnoreCase))
                return "A reporting cycle for this report already starts on the selected date.";
            if (oracle.Message.Contains("uq_rt_report_cycles_report_definition_id_period_end", StringComparison.OrdinalIgnoreCase))
                return "A reporting cycle for this report already ends on the selected date.";
            if (oracle.Message.Contains("uq_rt_report_cycles_report_definition_id_period_key", StringComparison.OrdinalIgnoreCase))
                return "This reporting cycle already exists for the selected report and period.";
            return "A record with the same unique information already exists.";
        }
        if (exception is DbUpdateException) return "The record could not be saved because it conflicts with a recent change. Refresh and try again.";
        return exception.Message;
    }
}
