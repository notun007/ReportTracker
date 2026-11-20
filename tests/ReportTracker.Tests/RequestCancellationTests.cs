using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReportTracker.Api.Infrastructure;

namespace ReportTracker.Tests;

public sealed class RequestCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_cancellation_is_handled_without_writing_to_disconnected_browser(bool taskCanceled)
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        context.Response.Body = new MemoryStream();
        var middleware = new RequestCancellationMiddleware(_ => throw Canceled(taskCanceled), NullLogger<RequestCancellationMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Null(context.Response.ContentType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execution_cancellation_with_connected_browser_returns_safe_timeout_message(bool taskCanceled)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "timeout-test" };
        context.Response.Body = new MemoryStream();
        var middleware = new RequestCancellationMiddleware(_ => throw Canceled(taskCanceled), NullLogger<RequestCancellationMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(408, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("request_timed_out", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Request timed out", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(RequestCancellationResponse.Message, json.RootElement.GetProperty("detail").GetString());
        Assert.Equal("timeout-test", json.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("internal query", body);
        Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Theory]
    [InlineData(false, 408)]
    [InlineData(true, 499)]
    public async Task Global_handler_uses_the_same_rules_without_writing_with_a_canceled_handler_token(bool disconnected, int expectedStatus)
    {
        using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, RequestAborted = new CancellationToken(disconnected) };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new TaskCanceledException(), new CancellationToken(true)));
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(disconnected, context.Response.Body.Length == 0);
    }

    [Fact]
    public async Task Cancellation_after_response_started_does_not_append_problem_json()
    {
        var feature = new StartedResponse();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        var middleware = new RequestCancellationMiddleware(_ => throw new TaskCanceledException(), NullLogger<RequestCancellationMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(200, feature.StatusCode);
        Assert.Equal(0, feature.Body.Length);
    }

    [Fact]
    public async Task Other_errors_continue_to_existing_exception_handling()
    {
        var failure = new InvalidOperationException("business or programming error");
        var middleware = new RequestCancellationMiddleware(_ => throw failure, NullLogger<RequestCancellationMiddleware>.Instance);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext())));
    }

    private static OperationCanceledException Canceled(bool taskCanceled) => taskCanceled
        ? new TaskCanceledException("internal query canceled") : new OperationCanceledException("internal query canceled");

    private sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
