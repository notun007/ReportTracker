using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Api.Infrastructure;
using ReportTracker.Application.Abstractions;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure;

var migrateAccess = args.Contains("--migrate-access");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate-access").ToArray());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ReportTracker.Api.Infrastructure.GlobalExceptionHandler>();
builder.Services.AddControllers(options => options.Filters.Add<ValidateSessionAntiforgery>()).AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddCors(options => options.AddPolicy("Web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("api", limiter =>
{
    limiter.PermitLimit = 120;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
builder.Services.AddAuthentication("SessionOrBearer")
    .AddPolicyScheme("SessionOrBearer", "Local session or bank SSO token", options =>
        options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme : CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ReportTracker.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        options.Events.OnValidatePrincipal = async context =>
        {
            var validId = Guid.TryParse(context.Principal?.FindFirst("user_id")?.Value, out var id);
            var db = context.HttpContext.RequestServices.GetRequiredService<ReportTrackerDbContext>();
            if (!validId || !await db.Users.AnyAsync(x => x.Id == id && x.IsActive, context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    }).AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = audience;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});
builder.Services.AddAuthorization(options => options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().RequireClaim("user_id").Build());

var app = builder.Build();
if (migrateAccess)
{
    await using var scope=app.Services.CreateAsyncScope();
    var db=scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>();
    if (await db.AuditEvents.AnyAsync(x => x.Action == "AccessControlUpgraded"))
    {
        Console.WriteLine("Access catalogue already upgraded; existing role changes were preserved.");
        return;
    }
    await using var transaction=await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"rt_user_roles\" IN EXCLUSIVE MODE");
    if (await db.AuditEvents.AnyAsync(x => x.Action == "AccessControlUpgraded"))
    {
        Console.WriteLine("Access catalogue already upgraded; existing role changes were preserved.");
        return;
    }
    var actor=await (from user in db.Users join assignment in db.UserRoles on user.Id equals assignment.UserId
                     join mapping in db.RolePermissions on assignment.RoleId equals mapping.RoleId
                     join permission in db.Permissions on mapping.PermissionId equals permission.Id
                     where user.IsActive && assignment.IsActive && !assignment.DivisionId.HasValue && !assignment.ReportDefinitionId.HasValue &&
                         (permission.Code == "administration.manage" || permission.Code == "roles.manage") select user.Id).FirstOrDefaultAsync();
    if (actor == Guid.Empty) throw new InvalidOperationException("No active global access administrator was found. Review administrator grants before upgrading.");
    await ReportTracker.Infrastructure.Services.AccessControlSeed.PrepareAsync(db,true);
    db.AuditEvents.Add(new AuditEvent { EntityType="AccessControl",EntityId=actor,ActorUserId=actor,Action="AccessControlUpgraded",
        DataJson="{\"preserved\":\"Existing scoped grants, period assignments and contributor snapshots\",\"roles\":5}" });
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine("Access catalogue upgraded. Existing scopes and operational assignments were preserved.");
    return;
}
app.UseExceptionHandler();
app.UseMiddleware<RequestCancellationMiddleware>();
app.UseHttpsRedirection();
app.UseCors("Web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers().RequireRateLimiting("api");
app.Run();

public partial class Program;
