using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class DivisionCreationTests
{
    [Theory]
    [InlineData("1001")]
    [InlineData(" 1001 ")]
    public async Task Existing_code_returns_clear_conflict_without_inserting_or_auditing(string code)
    {
        await using var db = CreateDb();
        var actor = await SeedAdministrator(db);
        var controller = Controller(db, actor);

        var result = await controller.CreateDivision(new CreateDivisionRequest(code, "Another division"), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("division_code_exists", problem.Extensions["code"]);
        Assert.Contains("1001", problem.Detail);
        Assert.Single(await db.Divisions.ToListAsync());
        Assert.Equal("Original division", (await db.Divisions.SingleAsync()).Name);
        Assert.Empty(await db.AuditEvents.ToListAsync());
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State == EntityState.Added);
    }

    [Fact]
    public async Task New_code_is_normalized_saved_and_audited()
    {
        await using var db = CreateDb();
        var actor = await SeedAdministrator(db);
        var result = await Controller(db, actor).CreateDivision(new CreateDivisionRequest(" ict ", " ICT Division "), default);

        Assert.IsType<CreatedResult>(result);
        var division = await db.Divisions.SingleAsync(x => x.Code == "ICT");
        Assert.Equal("ICT Division", division.Name);
        var audit = await db.AuditEvents.SingleAsync();
        Assert.Equal("DivisionCreated", audit.Action);
        Assert.Equal(division.Id, audit.EntityId);
        Assert.Equal(actor, audit.ActorUserId);
    }

    private static ReportTrackerDbContext CreateDb() => new(new DbContextOptionsBuilder<ReportTrackerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SetupController Controller(ReportTrackerDbContext db, Guid actor) =>
        new(db, new TrackingAccess(db, new Actor(actor)), new PasswordHasher<AppUser>());

    private static async Task<Guid> SeedAdministrator(ReportTrackerDbContext db)
    {
        var division = new Division { Code = "1001", Name = "Original division" };
        var user = new AppUser { EmployeeId = "ADMIN", DisplayName = "Administrator", Email = "admin@example.test", DivisionId = division.Id };
        var role = new AppRole { Name = "System Administrator" };
        var permission = new Permission { Code = "administration.manage", Name = "Manage administration" };
        db.AddRange(division, user, role, permission,
            new UserRole { UserId = user.Id, RoleId = role.Id },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed record Actor(Guid UserId) : ICurrentUser;
}
