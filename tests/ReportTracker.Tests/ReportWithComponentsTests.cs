using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class ReportWithComponentsTests
{
    [Fact]
    public async Task Report_responsibilities_version_and_components_are_saved_in_one_unit_of_work()
    {
        var counter = new Saves(); await using var db = Db(counter); var f = await Seed(db); var before = counter.Count;
        Assert.IsType<CreatedResult>(await Controller(db, f.Actor.Id).CreateReport(Request(f), default));
        Assert.Equal(before + 1, counter.Count);
        var report = await db.ReportDefinitions.SingleAsync(); var version = await db.DefinitionVersions.SingleAsync();
        Assert.Equal(f.C.Id, report.ReceivingDivisionId); Assert.Equal(f.C.Id, report.OwnerDivisionId); Assert.Equal(f.D.Id, report.SubmittingDivisionId);
        var components = await db.ComponentDefinitions.OrderBy(x => x.Sequence).ToListAsync(); Assert.Equal(2, components.Count);
        Assert.All(components, c => { Assert.Equal(version.Id, c.DefinitionVersionId); Assert.Equal(f.C.Id, c.ReceivingDivisionId); });
        Assert.Equal(f.A.Id, components[0].ProviderDivisionId); Assert.Equal(f.B.Id, components[1].ProviderDivisionId);
        Assert.All(components, c => Assert.Equal(version.ComponentDeadlineOffsetDays,
            JsonDocument.Parse(c.DeadlineRuleJson!).RootElement.GetProperty("offsetDays").GetInt32()));
        Assert.False(components[1].IsRequired);
        Assert.Single(await db.ResponsibilityAssignments.ToListAsync()); Assert.Equal(3, await db.AuditEvents.CountAsync());
    }

    [Theory]
    [InlineData("officer")]
    [InlineData("duplicate")]
    [InlineData("lead-days")]
    [InlineData("receiver")]
    [InlineData("deputy")]
    public async Task Invalid_bundle_creates_no_partial_report_components_assignments_or_audits(string failure)
    {
        var counter = new Saves(); await using var db = Db(counter); var f = await Seed(db); var request = Request(f); var before = counter.Count;
        var components = request.Components!.ToArray();
        switch (failure)
        {
            case "officer": components[1] = components[1] with { PrimaryOfficerId = f.OfficerA.Id }; break;
            case "duplicate": components[1] = components[1] with { Name = "  COMPONENT A " }; break;
            case "lead-days": request = request with { ComponentDeadlineOffsetDays = -1 }; break;
            case "receiver": request = request with { ReceivingDivisionId = Guid.NewGuid() }; break;
            case "deputy": components[1] = components[1] with { DeputyOfficerId = components[1].PrimaryOfficerId }; break;
        }
        await Assert.ThrowsAsync<TrackingValidationException>(() => Controller(db, f.Actor.Id).CreateReport(request with { Components = components }, default));
        Assert.Equal(before, counter.Count); Assert.Empty(await db.ReportDefinitions.ToListAsync()); Assert.Empty(await db.ComponentDefinitions.ToListAsync());
        Assert.Empty(await db.ResponsibilityAssignments.ToListAsync()); Assert.Empty(await db.AuditEvents.ToListAsync());
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State == EntityState.Added);
    }

    [Fact]
    public async Task Separate_component_management_cannot_override_a_reports_common_receiver()
    {
        await using var db = Db(new Saves()); var f = await Seed(db);
        await Controller(db, f.Actor.Id).CreateReport(Request(f), default); var report = await db.ReportDefinitions.SingleAsync();
        var controller = new ComponentsController(db, new TrackingAccess(db, new Actor(f.Actor.Id)));
        var request = new SaveComponentRequest("Other", null, true, 3, f.A.Id, f.OfficerA.Id, null, f.D.Id, false, false, 2);
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.Create(report.Id, request, default));
        Assert.Equal(2, await db.ComponentDefinitions.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(365)]
    public async Task New_components_use_report_lead_days_instead_of_the_clients_value(int leadDays)
    {
        await using var db = Db(new Saves()); var f = await Seed(db);
        await Controller(db, f.Actor.Id).CreateReport(Request(f) with { ComponentDeadlineOffsetDays = leadDays }, default);
        var report = await db.ReportDefinitions.SingleAsync();
        var controller = new ComponentsController(db, new TrackingAccess(db, new Actor(f.Actor.Id)));
        var request = new SaveComponentRequest("New input", null, true, 3, f.A.Id, f.OfficerA.Id, null, f.C.Id, false, false, 2);
        var result = await controller.Create(report.Id, request, default);
        var created = Assert.IsType<ComponentItem>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(leadDays, created.DeadlineOffsetDays);
        var stored = await db.ComponentDefinitions.SingleAsync(x => x.Id == created.Id);
        using var json = JsonDocument.Parse(stored.DeadlineRuleJson!);
        Assert.Equal(leadDays, json.RootElement.GetProperty("offsetDays").GetInt32());
    }

    private sealed class Saves : SaveChangesInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { Count++; return ValueTask.FromResult(result); }
    }
    private static ReportTrackerDbContext Db(Saves counter) => new(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(counter).Options);
    private sealed record Actor(Guid UserId) : ICurrentUser;
    private static SetupController Controller(ReportTrackerDbContext db, Guid actor) => new(db, new TrackingAccess(db, new Actor(actor)), new PasswordHasher<AppUser>());
    private sealed record Fixture(Division A, Division B, Division C, Division D, AppUser Actor, AppUser OfficerA, AppUser OfficerB, AppUser Submitter);
    private static CreateReportRequest Request(Fixture f) => new("ATM", "Monthly ATM report", null, f.C.Id, "BB", null, "Monthly", new TimeOnly(17, 0), 1,
        f.D.Id, f.Submitter.Id, null, f.C.Id,
        [new("Component A", "ATM figures", true, 1, f.A.Id, f.OfficerA.Id, null, true, true, 2),
         new("Component B", null, false, 2, f.B.Id, f.OfficerB.Id, null, false, true, 1)]);
    private static async Task<Fixture> Seed(ReportTrackerDbContext db)
    {
        Division Division(string code) => new() { Code = code, Name = "Division " + code };
        var a = Division("A"); var b = Division("B"); var c = Division("C"); var d = Division("D");
        AppUser User(string name, Division division) => new() { EmployeeId = name, DisplayName = name, Email = name + "@example.test", DivisionId = division.Id };
        var actor = User("Coordinator", c); var officerA = User("OfficerA", a); var officerB = User("OfficerB", b); var submitter = User("Submitter", d);
        var role = new AppRole { Name = "Coordinator" }; var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
        db.AddRange(a, b, c, d, actor, officerA, officerB, submitter, role, permission, new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
            new UserRole { UserId = actor.Id, RoleId = role.Id, DivisionId = c.Id });
        await db.SaveChangesAsync(); return new(a, b, c, d, actor, officerA, officerB, submitter);
    }
}
