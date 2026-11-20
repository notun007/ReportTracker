using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class ComponentTeamsTests
{
    [Fact]
    public async Task Preparing_division_manager_sees_only_its_latest_components_and_active_members_across_reports()
    {
        await using var f = await Fixture.Create();
        var result = await f.Directory(f.ManagerA);
        Assert.Equal(new[] { f.A1.Id, f.A2.Id }.Order(), result.Components.Select(x => x.Component.Id).Order());
        Assert.Equal(2, result.Components.Select(x => x.ReportId).Distinct().Count());
        Assert.All(result.Components, x => Assert.Equal(f.A.Id, x.Component.ProviderDivisionId));
        Assert.All(result.Members, x => Assert.Equal(f.A.Id, x.DivisionId));
        Assert.DoesNotContain(result.Members, x => x.Id == f.Inactive.Id);
        Assert.DoesNotContain(result.Components, x => x.Component.Id == f.Historical.Id);
        Assert.Empty((await f.Directory(f.ReportOwner)).Components);
        Assert.Equal(f.B1.Id, Assert.Single((await f.Directory(f.ManagerB)).Components).Component.Id);
        Assert.Equal(3, (await f.Directory(f.Global)).Components.Count);
    }

    [Fact]
    public async Task Saving_uses_preparing_division_authority_and_preserves_existing_periods_and_team_rules()
    {
        await using var f = await Fixture.Create();
        var previousTask = f.Task.RowVersion.ToArray(); var previousTeam = f.Task.ContributorsJson;
        var request = new SetComponentContributorsRequest([new(f.ManagerA.Id, "LeadPreparer"),
            new(f.Preparer.Id, "Preparer"), new(f.Preparer.Id, "Reviewer"), new(f.Preparer.Id, "BackupPreparer")]);
        foreach (var actor in new[] { f.ReportOwner, f.ManagerB, f.Preparer })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Components(actor).SetContributors(f.Report1.Id, f.A1.Id, request, default));
        Assert.Empty(f.Db.AuditEvents);
        var response = await f.Components(f.ManagerA).SetContributors(f.Report1.Id, f.A1.Id, request, default);
        var saved = Assert.IsType<ComponentItem>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(request.Contributors, saved.Contributors);
        Assert.Equal(f.ManagerA.Id, saved.PrimaryAssigneeId);
        Assert.Equal(f.Preparer.Id, f.Task.PrimaryAssigneeId);
        Assert.Equal(previousTask, f.Task.RowVersion); Assert.Equal(previousTeam, f.Task.ContributorsJson);
        Assert.Equal(f.ManagerA.Id, (await f.Db.AuditEvents.SingleAsync()).ActorUserId);
    }

    [Fact]
    public async Task Assignment_membership_alone_does_not_grant_team_management()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Directory(f.Preparer));
        f.ManagerA.IsActive = false; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Directory(f.ManagerA));
    }

    [Fact]
    public async Task Report_scope_and_actual_division_membership_are_both_enforced()
    {
        await using var f = await Fixture.Create();
        var grant = await f.Db.UserRoles.SingleAsync(x => x.UserId == f.ManagerA.Id);
        grant.ReportDefinitionId = f.Report1.Id; await f.Db.SaveChangesAsync();
        Assert.Equal(f.A1.Id, Assert.Single((await f.Directory(f.ManagerA)).Components).Component.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Components(f.ManagerA).SetContributors(f.Report2.Id, f.A2.Id,
            new([new(f.ManagerA.Id, "LeadPreparer")]), default));
        grant.DivisionId = f.B.Id; await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Directory(f.ManagerA)).Components);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("no-lead")]
    [InlineData("two-leads")]
    [InlineData("foreign-member")]
    [InlineData("duplicate-role")]
    public async Task Team_validation_remains_unchanged(string scenario)
    {
        await using var f = await Fixture.Create();
        ComponentContributorItem[] contributors = scenario switch
        {
            "empty" => [],
            "no-lead" => [new(f.Preparer.Id, "Preparer")],
            "two-leads" => [new(f.Preparer.Id, "LeadPreparer"), new(f.ManagerA.Id, "LeadPreparer")],
            "foreign-member" => [new(f.Preparer.Id, "LeadPreparer"), new(f.ManagerB.Id, "Preparer")],
            _ => [new(f.Preparer.Id, "LeadPreparer"), new(f.Preparer.Id, "Reviewer"), new(f.Preparer.Id, "Reviewer")]
        };
        await Assert.ThrowsAsync<TrackingValidationException>(() => f.Components(f.ManagerA).SetContributors(f.Report1.Id, f.A1.Id, new(contributors), default));
        Assert.Empty(f.Db.AuditEvents);
    }

    [Fact]
    public async Task Definition_can_be_created_before_the_preparing_division_enters_its_team()
    {
        await using var f = await Fixture.Create();
        var response = await f.Components(f.ReportOwner).Create(f.Report1.Id,
            new("New component", null, f.A.Id, null, null, f.C.Id, Contributors: []), default);
        var created = Assert.IsType<ComponentItem>(Assert.IsType<CreatedResult>(response.Result).Value);
        Assert.Null(created.PrimaryAssigneeId); Assert.Empty(created.Contributors!);
        Assert.Contains((await f.Directory(f.ManagerA)).Components, x => x.Component.Id == created.Id);
        await f.Components(f.ManagerA).SetContributors(f.Report1.Id, created.Id, new([new(f.Preparer.Id, "LeadPreparer")]), default);
        Assert.Equal(f.Preparer.Id, (await f.Db.ComponentDefinitions.FindAsync(created.Id))!.PrimaryAssigneeId);
    }

    [Fact]
    public async Task Direct_member_actions_persist_and_reload_without_replacing_other_members_or_period_assignments()
    {
        await using var f = await Fixture.Create();
        var taskVersion = f.Task.RowVersion.ToArray(); var taskTeam = f.Task.ContributorsJson;
        var endpoint = f.Components(f.ManagerA);
        await endpoint.SaveTeamMember(f.Report1.Id, f.A1.Id, f.ManagerA.Id, new(["Preparer", "Reviewer"]), default);
        var other = new AppUser { EmployeeId = "OtherA", DisplayName = "Other A", Email = "other@example.test", DivisionId = f.A.Id };
        f.Db.Users.Add(other); await f.Db.SaveChangesAsync();
        await endpoint.SaveTeamMember(f.Report1.Id, f.A1.Id, other.Id, new(["Preparer"]), default);
        await endpoint.SaveTeamMember(f.Report1.Id, f.A1.Id, f.ManagerA.Id, new(["BackupPreparer", "Reviewer"]), default);
        var reloaded = (await f.Directory(f.ManagerA)).Components.Single(x => x.Component.Id == f.A1.Id).Component;
        Assert.Contains(reloaded.Contributors!, x => x.UserId == f.Preparer.Id && x.Role == "LeadPreparer");
        Assert.Contains(reloaded.Contributors!, x => x.UserId == other.Id && x.Role == "Preparer");
        Assert.Equal(2, reloaded.Contributors!.Count(x => x.UserId == f.ManagerA.Id));
        Assert.DoesNotContain(reloaded.Contributors!, x => x.UserId == f.ManagerA.Id && x.Role == "Preparer");
        await endpoint.RemoveTeamMember(f.Report1.Id, f.A1.Id, f.ManagerA.Id, default);
        var stored = await f.Db.ComponentDefinitions.AsNoTracking().SingleAsync(x => x.Id == f.A1.Id);
        var contributors = ComponentValidation.Contributors(stored.ContributorsJson, stored.PrimaryAssigneeId, stored.AlternateAssigneeId);
        Assert.DoesNotContain(contributors, x => x.UserId == f.ManagerA.Id);
        Assert.Contains(contributors, x => x.UserId == other.Id);
        Assert.Equal(4, await f.Db.AuditEvents.CountAsync());
        Assert.All(await f.Db.AuditEvents.ToListAsync(), x => Assert.Equal(f.ManagerA.Id, x.ActorUserId));
        Assert.Equal(taskVersion, f.Task.RowVersion); Assert.Equal(taskTeam, f.Task.ContributorsJson);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("other-division")]
    [InlineData("preparer")]
    public async Task Direct_member_save_and_remove_enforce_the_same_division_authority(string scenario)
    {
        await using var f = await Fixture.Create();
        var actor = scenario == "owner" ? f.ReportOwner : scenario == "other-division" ? f.ManagerB : f.Preparer;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Components(actor).SaveTeamMember(f.Report1.Id, f.A1.Id,
            f.ManagerA.Id, new(["Preparer"]), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Components(actor).RemoveTeamMember(f.Report1.Id, f.A1.Id, f.Preparer.Id, default));
        Assert.Empty(f.Db.AuditEvents);
    }

    [Theory]
    [InlineData("empty-roles")]
    [InlineData("duplicate-role")]
    [InlineData("unknown-role")]
    [InlineData("missing-lead")]
    [InlineData("foreign-member")]
    [InlineData("inactive-member")]
    public async Task Direct_member_validation_failure_does_not_change_the_saved_team(string scenario)
    {
        await using var f = await Fixture.Create();
        var member = scenario == "foreign-member" ? f.ManagerB.Id : scenario == "inactive-member" ? f.Inactive.Id :
            scenario == "missing-lead" ? f.Preparer.Id : f.ManagerA.Id;
        string[] roles = scenario == "empty-roles" ? [] : scenario == "duplicate-role" ? ["Preparer", "Preparer"] :
            scenario == "unknown-role" ? ["Administrator"] : ["Preparer"];
        var previous = f.A1.ContributorsJson;
        await Assert.ThrowsAsync<TrackingValidationException>(() => f.Components(f.ManagerA).SaveTeamMember(f.Report1.Id, f.A1.Id, member, new(roles), default));
        Assert.Equal(previous, f.A1.ContributorsJson); Assert.Equal(f.Preparer.Id, f.A1.PrimaryAssigneeId);
        Assert.Empty(f.Db.AuditEvents);
    }

    [Fact]
    public async Task Direct_remove_preserves_existing_lead_validation_and_reports_a_stale_member()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<TrackingValidationException>(() => f.Components(f.ManagerA).RemoveTeamMember(f.Report1.Id, f.A1.Id, f.Preparer.Id, default));
        await Assert.ThrowsAsync<TrackingConflictException>(() => f.Components(f.ManagerA).RemoveTeamMember(f.Report1.Id, f.A1.Id, f.ManagerA.Id, default));
        Assert.Empty(f.Db.AuditEvents); Assert.Equal(f.Preparer.Id, f.A1.PrimaryAssigneeId);
    }

    [Fact]
    public async Task First_member_can_be_saved_directly_to_an_unassigned_component()
    {
        await using var f = await Fixture.Create();
        var created = Assert.IsType<ComponentItem>(Assert.IsType<CreatedResult>((await f.Components(f.ReportOwner).Create(f.Report1.Id,
            new("Direct first member", null, f.A.Id, null, null, f.C.Id), default)).Result).Value);
        await f.Components(f.ManagerA).SaveTeamMember(f.Report1.Id, created.Id, f.ManagerA.Id, new(["LeadPreparer", "Preparer"]), default);
        var reloaded = (await f.Directory(f.ManagerA)).Components.Single(x => x.Component.Id == created.Id).Component;
        Assert.Equal(f.ManagerA.Id, reloaded.PrimaryAssigneeId);
        Assert.Equal(2, reloaded.Contributors!.Count);
    }

    private sealed record Actor(Guid UserId) : ICurrentUser;
    private sealed class Fixture : IAsyncDisposable
    {
        public ReportTrackerDbContext Db { get; } = new(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Division A { get; } = new() { Code = "A", Name = "Preparing A" };
        public Division B { get; } = new() { Code = "B", Name = "Preparing B" };
        public Division C { get; } = new() { Code = "C", Name = "Coordinating" };
        public AppUser ManagerA = null!, ManagerB = null!, ReportOwner = null!, Global = null!, Preparer = null!, Inactive = null!;
        public ReportDefinition Report1 = null!, Report2 = null!;
        public ComponentDefinition A1 = null!, A2 = null!, B1 = null!, Historical = null!;
        public TrackingObligation Task = null!;
        public TrackingAccess Access(AppUser actor) => new(Db, new Actor(actor.Id));
        public ComponentsController Components(AppUser actor) => new(Db, Access(actor));
        public async Task<ComponentTeamDirectory> Directory(AppUser actor) => Assert.IsType<ComponentTeamDirectory>(Assert.IsType<OkObjectResult>((await new ComponentTeamsController(Db, Access(actor)).Get(default)).Result).Value);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            AppUser User(string name, Division division) => new() { EmployeeId = name, DisplayName = name, Email = name + "@example.test", DivisionId = division.Id };
            f.ManagerA = User("ManagerA", f.A); f.ManagerB = User("ManagerB", f.B); f.ReportOwner = User("Owner", f.C);
            f.Global = User("Global", f.C); f.Preparer = User("Preparer", f.A); f.Inactive = User("Inactive", f.A); f.Inactive.IsActive = false;
            ReportDefinition Report(string code) => new() { Code = code, Name = code, OwnerDivisionId = f.C.Id, ReceivingDivisionId = f.C.Id, BbRecipient = "Recipient" };
            f.Report1 = Report("R1"); f.Report2 = Report("R2");
            var v1 = new DefinitionVersion { ReportDefinitionId = f.Report1.Id, VersionNumber = 1 };
            var old = new DefinitionVersion { ReportDefinitionId = f.Report2.Id, VersionNumber = 1 };
            var v2 = new DefinitionVersion { ReportDefinitionId = f.Report2.Id, VersionNumber = 2 };
            ComponentDefinition Component(string name, DefinitionVersion version, Division division) => new() { Name = name,
                DefinitionVersionId = version.Id, ProviderDivisionId = division.Id, ReceivingDivisionId = f.C.Id,
                PrimaryAssigneeId = division == f.A ? f.Preparer.Id : f.ManagerB.Id };
            f.A1 = Component("A1", v1, f.A); f.B1 = Component("B1", v1, f.B); f.A2 = Component("A2", v2, f.A); f.Historical = Component("Old", old, f.A);
            var cycle = new ReportCycle { ReportDefinition = f.Report1, PeriodKey = "2026-11", PeriodLabel = "November" };
            f.Task = new TrackingObligation { ReportCycle = cycle, Name = "A1", AccountableDivisionId = f.A.Id,
                PrimaryAssigneeId = f.Preparer.Id, Recipient = "Receiving", ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Preparer.Id, "LeadPreparer") }) };
            var role = new AppRole { Name = "Report Coordinator" }; var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
            f.Db.AddRange(f.A, f.B, f.C, f.ManagerA, f.ManagerB, f.ReportOwner, f.Global, f.Preparer, f.Inactive, f.Report1, f.Report2,
                v1, old, v2, f.A1, f.B1, f.A2, f.Historical, f.Task, role, permission, new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            foreach (var user in new[] { f.ManagerA, f.ManagerB, f.ReportOwner, f.Global })
                f.Db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, DivisionId = user == f.Global ? null : user.DivisionId });
            await f.Db.SaveChangesAsync(); await AccessControlSeed.PrepareAsync(f.Db, true); await f.Db.SaveChangesAsync(); return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
