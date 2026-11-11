using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class ReportingCycleLookupTests
{
    [Fact]
    public async Task Saved_cycles_are_scoped_to_report_and_preserve_historical_assignments_and_effective_deadlines()
    {
        await using var db = CreateDb();
        var (actor, report, other, officer, division) = await Seed(db);
        var old = Cycle(report, "2026-10", new DateOnly(2026, 10, 1));
        var current = Cycle(report, "2026-11", new DateOnly(2026, 11, 1));
        var due = new DateTime(2026, 12, 3, 11, 0, 0, DateTimeKind.Utc);
        current.Obligations.Add(new TrackingObligation { Name = "Final BB task", Type = ObligationType.External,
            AccountableDivisionId = division.Id, PrimaryOfficerId = officer.Id, Recipient = "BB",
            OriginalDueAtUtc = due.AddDays(-1), EffectiveDueAtUtc = due,
            ReceiptStatus = ReceiptStatus.ReceiptConfirmed, EvidenceStatus = EvidenceStatus.Verified });
        db.AddRange(old, current, Cycle(other, "2026-11", new DateOnly(2026, 11, 1)));
        await db.SaveChangesAsync();

        var response = await Controller(db, actor).Cycles(report.Id, default);

        var rows = Assert.IsType<List<ReportingCycleItem>>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(new[] { "2026-11", "2026-10" }, rows.Select(x => x.PeriodKey));
        var task = Assert.Single(rows[0].Obligations);
        Assert.Equal(officer.Id, task.PrimaryOfficerId);
        Assert.Equal("Former officer", task.PrimaryOfficerName);
        Assert.Equal(division.Name, task.DivisionName);
        Assert.Equal(due, task.DueAtUtc);
        Assert.Equal("BB", task.Type);
        Assert.True(task.ReceiptRequired);
        Assert.True(task.EvidenceRequired);
        Assert.Empty(await db.AuditEvents.ToListAsync());
    }

    [Fact]
    public async Task Scoped_coordinator_cannot_read_another_reports_cycles()
    {
        await using var db = CreateDb();
        var (actor, _, other, _, _) = await Seed(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(db, actor).Cycles(other.Id, default));
    }

    [Fact]
    public async Task Report_without_cycles_returns_empty_list()
    {
        await using var db = CreateDb();
        var (actor, report, _, _, _) = await Seed(db);
        var response = await Controller(db, actor).Cycles(report.Id, default);
        Assert.Empty(Assert.IsType<List<ReportingCycleItem>>(Assert.IsType<OkObjectResult>(response.Result).Value));
    }

    private static ReportTrackerDbContext CreateDb() => new(new DbContextOptionsBuilder<ReportTrackerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SetupController Controller(ReportTrackerDbContext db, Guid actor) =>
        new(db, new TrackingAccess(db, new Actor(actor)), new PasswordHasher<AppUser>());
    private static ReportCycle Cycle(ReportDefinition report, string key, DateOnly start) => new()
    {
        ReportDefinitionId = report.Id, PeriodKey = key, PeriodLabel = key, PeriodStart = start, PeriodEnd = start.AddMonths(1).AddDays(-1)
    };
    private static async Task<(Guid Actor, ReportDefinition Report, ReportDefinition Other, AppUser Officer, Division Division)> Seed(ReportTrackerDbContext db)
    {
        var division = new Division { Code = "RIT", Name = "Reporting division" };
        var actor = new AppUser { EmployeeId = "COORD", DisplayName = "Coordinator", Email = "coord@example.test", DivisionId = division.Id };
        var officer = new AppUser { EmployeeId = "OLD", DisplayName = "Former officer", Email = "old@example.test", DivisionId = division.Id, IsActive = false };
        var report = new ReportDefinition { Code = "RD-1001", Name = "RIT", OwnerDivisionId = division.Id, BbRecipient = "BB" };
        var other = new ReportDefinition { Code = "OTHER", Name = "Other report", OwnerDivisionId = division.Id, BbRecipient = "BB" };
        var role = new AppRole { Name = "Scoped coordinator" };
        var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
        db.AddRange(division, actor, officer, report, other, role, permission,
            new UserRole { UserId = actor.Id, RoleId = role.Id, DivisionId = division.Id, ReportDefinitionId = report.Id },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();
        return (actor.Id, report, other, officer, division);
    }
    private sealed record Actor(Guid UserId) : ICurrentUser;
}
