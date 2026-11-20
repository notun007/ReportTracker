using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ReportTracker.Api.Infrastructure;

public static class DatabaseConnectionFailure
{
    public const string Code = "database_unavailable";
    public const string Message = "ReportTracker cannot connect to its database right now. Please retry. If the problem continues, contact your system administrator.";

    public static bool IsUnavailable(Exception exception)
    {
        // Recognize connection/availability failures, including the ODP.NET wrapper in
        // ORA-50201. SQL errors and constraint violations must keep their own handling.
        if (exception is OracleException { Number: 1012 or 1033 or 1034 or 1089 or 1090 or
            3113 or 3114 or 3135 or 12154 or 12170 or 12514 or 12537 or 12541 or 12543 or
            12545 or 12547 or 12560 or 12570 or 12571 or 50201 }) return true;
        if (exception is AggregateException aggregate)
            return aggregate.InnerExceptions.Any(IsUnavailable);
        return exception.InnerException is { } inner && IsUnavailable(inner);
    }

    public static ProblemDetails Problem(HttpContext context) => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Database unavailable",
        Detail = Message,
        Extensions = { ["code"] = Code, ["traceId"] = context.TraceIdentifier }
    };

    public static void SetHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.RetryAfter = "30";
    }
}
