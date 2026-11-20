using System.Data;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Api.Infrastructure;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ReportTrackerDbContext db, IAntiforgery antiforgery, IPasswordHasher<AppUser> hasher,
    TrackingAccess access, IWebHostEnvironment environment, IConfiguration configuration, ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("session")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionResponse>> Session(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        SetCsrfCookie();
        try
        {
            return Ok(new SessionResponse(!await db.Users.AnyAsync(ct), await CurrentAsync(ct)));
        }
        catch (Exception exception) when (DatabaseConnectionFailure.IsUnavailable(exception))
        {
            logger.LogWarning(exception, "Workspace connection check failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            DatabaseConnectionFailure.SetHeaders(Response);
            return new ObjectResult(DatabaseConnectionFailure.Problem(HttpContext))
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                ContentTypes = { "application/problem+json" }
            };
        }
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionUser>> Setup(SetupRequest request, CancellationToken ct)
    {
        EnsureLocalEnabled();
        var localDevelopment = environment.IsDevelopment() && HttpContext.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);
        var setupKey = configuration["Authentication:SetupKey"];
        if (!localDevelopment && (string.IsNullOrWhiteSpace(setupKey) || request.SetupKey != setupKey))
            return Problem(statusCode: 403, title: "Initial setup requires the configured setup key.");
        AppUser? created = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // Oracle SERIALIZABLE can fail with ORA-08177 when concurrent application activity
            // changes a recently created data block. Serialize only first-time setup attempts by
            // locking the user table; normal reads, including the session check, remain available.
            if (db.Database.IsOracle())
                await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"rt_app_users\" IN EXCLUSIVE MODE", ct);
            if (await db.Users.AnyAsync(ct)) throw new TrackingConflictException("Initial setup is already complete. Sign in instead.");
            var division = new Division { Code = request.DivisionCode.Trim().ToUpperInvariant(), Name = request.DivisionName.Trim() };
            var user = new AppUser { EmployeeId = request.EmployeeId.Trim(), DisplayName = request.DisplayName.Trim(), Email = request.Email.Trim().ToLowerInvariant(), DivisionId = division.Id };
            db.AddRange(division,user);
            db.LocalCredentials.Add(new LocalCredential { UserId=user.Id,PasswordHash=hasher.HashPassword(user,request.Password) });
            var core = await AccessControlSeed.PrepareAsync(db,false,ct);
            db.UserRoles.AddRange(new UserRole { UserId=user.Id,RoleId=core["SYS_ADMIN"].Id },new UserRole { UserId=user.Id,RoleId=core["REPORT_ADMIN"].Id });
            db.AuditEvents.Add(new AuditEvent { EntityType = nameof(AppUser), EntityId = user.Id, ActorUserId = user.Id, Action = "InitialSetup", DataJson = "{\"roles\":[\"System Administrator\",\"Reporting Administrator\"]}" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            created = user;
        });
        await SignInAsync(created!);
        return Ok(await UserDtoAsync(created!, ct));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionUser>> Login(LoginRequest request, CancellationToken ct)
    {
        EnsureLocalEnabled();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email && x.IsActive, ct);
        var credential = user is null ? null : await db.LocalCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id, ct);
        if (user is null || credential is null || credential.LockedUntilUtc > DateTime.UtcNow)
            return Problem(statusCode: 401, title: "Sign-in failed. Check your credentials or try again later.");
        var verification = hasher.VerifyHashedPassword(user, credential.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            var lockUntil = DateTime.UtcNow.AddMinutes(15);
            await db.LocalCredentials.Where(x => x.Id == credential.Id).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1)
                .SetProperty(x => x.LockedUntilUtc, x => x.FailedAttempts + 1 >= 5 ? lockUntil : (DateTime?)null), ct);
            return Problem(statusCode: 401, title: "Sign-in failed. Check your credentials or try again later.");
        }
        var hash = verification == PasswordVerificationResult.SuccessRehashNeeded ? hasher.HashPassword(user, request.Password) : credential.PasswordHash;
        await db.LocalCredentials.Where(x => x.Id == credential.Id).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.FailedAttempts, 0).SetProperty(x => x.LockedUntilUtc, (DateTime?)null).SetProperty(x => x.PasswordHash, hash), ct);
        await SignInAsync(user);
        return Ok(await UserDtoAsync(user, ct));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private void EnsureLocalEnabled()
    {
        if (!configuration.GetValue("Authentication:LocalEnabled", true)) throw new UnauthorizedAccessException("Local sign-in is disabled. Use bank SSO.");
    }

    private Task SignInAsync(AppUser user) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("user_id", user.Id.ToString()), new Claim(ClaimTypes.Name, user.DisplayName)], CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

    private async Task<SessionUser?> CurrentAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == access.UserId && x.IsActive, ct);
        return user is null ? null : await UserDtoAsync(user, ct);
    }

    private async Task<SessionUser> UserDtoAsync(AppUser user, CancellationToken ct)
    {
        var permissions = await (from role in db.UserRoles join definition in db.Roles on role.RoleId equals definition.Id join grant in db.RolePermissions on role.RoleId equals grant.RoleId
                                 join permission in db.Permissions on grant.PermissionId equals permission.Id
                                 where role.UserId == user.Id && role.IsActive && definition.IsActive select permission.Code).Distinct().ToListAsync(ct);
        return new(user.Id, user.DisplayName, user.Email, user.DivisionId, permissions);
    }

    private void SetCsrfCookie()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("ReportTracker.XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false,
            Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true });
    }
}
