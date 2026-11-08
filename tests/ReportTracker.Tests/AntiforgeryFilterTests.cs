using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using ReportTracker.Api.Infrastructure;

namespace ReportTracker.Tests;

public sealed class AntiforgeryFilterTests
{
    [Fact]
    public async Task Invalid_token_returns_a_machine_readable_pre_action_rejection()
    {
        var antiforgery = new FakeAntiforgery(false);
        var context = Context("POST");
        await new ValidateSessionAntiforgery(antiforgery).OnAuthorizationAsync(context);
        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("csrf_token_invalid", problem.Extensions["code"]);
        Assert.Equal(1, antiforgery.ValidationCount);
    }

    [Fact]
    public async Task Valid_token_allows_the_action_to_run()
    {
        var antiforgery = new FakeAntiforgery(true);
        var context = Context("POST");
        await new ValidateSessionAntiforgery(antiforgery).OnAuthorizationAsync(context);
        Assert.Null(context.Result);
        Assert.Equal(1, antiforgery.ValidationCount);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Safe_requests_do_not_require_a_token(string method)
    {
        var antiforgery = new FakeAntiforgery(false);
        var context = Context(method);
        await new ValidateSessionAntiforgery(antiforgery).OnAuthorizationAsync(context);
        Assert.Null(context.Result);
        Assert.Equal(0, antiforgery.ValidationCount);
    }

    [Fact]
    public async Task Unauthenticated_bearer_header_does_not_bypass_validation()
    {
        var antiforgery = new FakeAntiforgery(false);
        var context = Context("POST");
        context.HttpContext.Request.Headers.Authorization = "Bearer invalid";
        await new ValidateSessionAntiforgery(antiforgery).OnAuthorizationAsync(context);
        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public async Task Authenticated_bearer_request_does_not_need_a_cookie_token()
    {
        var antiforgery = new FakeAntiforgery(false);
        var context = Context("POST");
        context.HttpContext.Request.Headers.Authorization = "Bearer validated";
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("user_id", Guid.NewGuid().ToString())], "Bearer"));
        await new ValidateSessionAntiforgery(antiforgery).OnAuthorizationAsync(context);
        Assert.Null(context.Result);
        Assert.Equal(0, antiforgery.ValidationCount);
    }

    private static AuthorizationFilterContext Context(string method)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    private sealed class FakeAntiforgery(bool valid) : IAntiforgery
    {
        public int ValidationCount { get; private set; }
        public Task ValidateRequestAsync(HttpContext context)
        {
            ValidationCount++;
            return valid ? Task.CompletedTask : Task.FromException(new AntiforgeryValidationException("Invalid token"));
        }
        public Task<bool> IsRequestValidAsync(HttpContext context) => Task.FromResult(valid);
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext context) => throw new NotSupportedException();
        public void SetCookieTokenAndHeader(HttpContext context) => throw new NotSupportedException();
    }
}
