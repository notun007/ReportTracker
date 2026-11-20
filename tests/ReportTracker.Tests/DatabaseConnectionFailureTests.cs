using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using ReportTracker.Api.Controllers;
using ReportTracker.Api.Infrastructure;
using ReportTracker.Application.Abstractions;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class DatabaseConnectionFailureTests
{
    [Theory]
    [InlineData(50201)]
    [InlineData(12170)]
    [InlineData(12541)]
    [InlineData(12514)]
    [InlineData(3113)]
    [InlineData(1034)]
    public async Task Connection_failures_return_safe_retryable_problem_details_even_when_wrapped(int number)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "connection-test" };
        context.Response.Body = new MemoryStream();
        var exception = new DbUpdateException("internal database details", new InvalidOperationException("provider wrapper", Oracle(number)));
        Assert.True(await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, default));
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("30", context.Response.Headers.RetryAfter.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("database_unavailable", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Database unavailable", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("connection-test", json.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("private-host", body);
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("ORA-", body);
        Assert.False(json.RootElement.TryGetProperty("setupRequired", out _));
    }

    [Theory]
    [InlineData(1, 409)]
    [InlineData(8177, 409)]
    [InlineData(942, 500)]
    [InlineData(904, 500)]
    public async Task Sql_and_constraint_errors_are_not_misreported_as_database_outages(int number, int status)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var exception = new DbUpdateException("write failed", Oracle(number));
        Assert.False(DatabaseConnectionFailure.IsUnavailable(exception));
        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, default);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task Session_action_handles_unavailable_database_without_returning_setup_or_throwing_to_middleware()
    {
        await using var db = new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseOracle("User Id=unused;Password=unused;Data Source=unused")
            .AddInterceptors(new FailedConnection()).Options);
        var context = new DefaultHttpContext();
        var controller = new AuthController(db, new Tokens(), null!, new TrackingAccess(db, new Actor()),
            new DevelopmentEnvironment(), new ConfigurationBuilder().Build(), NullLogger<AuthController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        var response = await controller.Session(default);
        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(503, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("database_unavailable", problem.Extensions["code"]);
        Assert.Equal(DatabaseConnectionFailure.Message, problem.Detail);
        Assert.Equal("30", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public void Unrelated_timeouts_and_generic_error_text_do_not_claim_an_oracle_outage()
    {
        Assert.False(DatabaseConnectionFailure.IsUnavailable(new TimeoutException("unrelated service timeout")));
        Assert.False(DatabaseConnectionFailure.IsUnavailable(new Exception("ORA-50201")));
        Assert.True(DatabaseConnectionFailure.IsUnavailable(new AggregateException(new Exception(), Oracle(12541))));
    }

    private static OracleException Oracle(int number)
    {
        // ODP.NET has no public exception constructor. Build deterministic provider errors
        // without depending on a real database or waiting for a network timeout.
        var constructor = typeof(OracleException).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(int), typeof(string), typeof(string), typeof(string), typeof(int)], null)!;
        return (OracleException)constructor.Invoke([number, "private-host Password=secret", "private-host Password=secret", "private-host Password=secret", 0]);
    }

    private sealed class FailedConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw Oracle(50201);
    }

    private sealed class Actor : ICurrentUser { public Guid UserId => Guid.Empty; }
    private sealed class Tokens : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context) => new("request", "cookie", "form", "header");
        public AntiforgeryTokenSet GetTokens(HttpContext context) => GetAndStoreTokens(context);
        public Task<bool> IsRequestValidAsync(HttpContext context) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext context) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext context) { }
    }

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ReportTracker.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
