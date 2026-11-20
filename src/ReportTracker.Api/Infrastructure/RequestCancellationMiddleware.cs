using Microsoft.AspNetCore.Mvc;

namespace ReportTracker.Api.Infrastructure;

public sealed class RequestCancellationMiddleware(RequestDelegate next, ILogger<RequestCancellationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            await RequestCancellationResponse.HandleAsync(context, logger);
        }
    }
}

public static class RequestCancellationResponse
{
    public const string Code = "request_timed_out";
    public const string Message = "The request took too long to complete. Refresh the screen to check the latest status before trying again.";

    public static async Task HandleAsync(HttpContext context, ILogger logger)
    {
        if (context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected browser cannot receive a message. Do not write using its
            // canceled token or turn normal navigation/cancellation into a server error.
            logger.LogDebug("Request canceled by client. TraceId: {TraceId}", context.TraceIdentifier);
            if (!context.Response.HasStarted) context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return;
        }

        logger.LogWarning("Request execution canceled before completion. TraceId: {TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted)
        {
            // Appending problem JSON to a response already in progress would corrupt it.
            context.Abort();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status408RequestTimeout,
            Title = "Request timed out",
            Detail = Message,
            Extensions = { ["code"] = Code, ["traceId"] = context.TraceIdentifier }
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
