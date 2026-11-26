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

public sealed class ReportResponsibilityAssignmentsTests
{
    [Fact]
    public async Task Loading_assignments_prefills_existing_submitter_and_only_lists_members_of_each_division()
    {
        await using var f = await Fixture.Create();
        var data = await f.Get();
        Assert.Equal(f.ControllerDivision.Id, data.Controller.DivisionId);
        Assert.Null(data.Controller.PrimaryAssigneeId);
        Assert.All(data.Controller.Assignees, x => Assert.Contains(x.Id, new[] { f.Manager.Id, f.Controller.Id, f.ControllerDeputy.Id }));
        Assert.Equal(f.Submitter.Id, data.FinalSubmission.PrimaryAssigneeId);
        Assert.Null(data.FinalSubmission.AlternateAssigneeId);
        Assert.Equal(2, data.Receiver.Assignees.Count);
        Assert.Null(data.Receiver.PrimaryAssigneeId);
        Assert.Empty(f.Db.AuditEvents);
        Assert.Single(f.Db.ResponsibilityAssignments);
    }

    [Fact]
    public async Task Saving_three_pairs_preserves_existing_periods_components_divisions_and_account_roles()
    {
        await using var f = await Fixture.Create();
        var request = f.Request() with { Receiver = new(f.Receiver.Id, f.ReceiverDeputy.Id), FinalSubmission = new(f.SubmitterDeputy.Id, f.Submitter.Id) };
        var taskVersion = f.Task.RowVersion.ToArray();
        var componentVersion = f.Component.RowVersion.ToArray();
        var grants = await f.Db.UserRoles.CountAsync();
        Assert.IsType<NoContentResult>(await f.Endpoint.Save(f.Report.Id, request, default));
        var data = await f.Get();
        Assert.Equal(f.Controller.Id, data.Controller.PrimaryAssigneeId);
        Assert.Equal(f.ReceiverDeputy.Id, data.Receiver.AlternateAssigneeId);
        Assert.Equal(f.SubmitterDeputy.Id, data.FinalSubmission.PrimaryAssigneeId);
        Assert.Equal(f.Submitter.Id, f.Task.PrimaryAssigneeId);
        Assert.Equal(taskVersion, f.Task.RowVersion);
        Assert.Equal(componentVersion, f.Component.RowVersion);
        Assert.Equal(f.ControllerDivision.Id, f.Report.OwnerDivisionId);
        Assert.Equal(f.ReceivingDivision.Id, f.Report.ReceivingDivisionId);
        Assert.Equal(f.SubmittingDivision.Id, f.Report.SubmittingDivisionId);
        Assert.Equal(grants, await f.Db.UserRoles.CountAsync());
        Assert.False(await new TrackingAccess(f.Db, new Actor(f.Controller.Id)).HasAsync("reports.manage", f.ControllerDivision.Id, f.Report.Id, default));
        Assert.Equal(3, await f.Db.ResponsibilityAssignments.CountAsync(x => !x.EffectiveToUtc.HasValue));
        Assert.Single(await f.Db.ResponsibilityAssignments.Where(x => x.EffectiveToUtc.HasValue).ToListAsync());
        var audit = await f.Db.AuditEvents.SingleAsync();
        Assert.Equal("ReportResponsibilityAssignmentsChanged", audit.Action);
        Assert.Equal(f.Manager.Id, audit.ActorUserId);
        // The existing receiving editor must show both officers stored by the new tab.
        var legacy = Assert.IsType<ReceivingOfficerDirectory>(Assert.IsType<OkObjectResult>((await new ReceivingOfficersController(f.Db, f.Access)
            .Get(f.Report.Id, f.ReceivingDivision.Id, default)).Result).Value);
        Assert.Equal(new[] { f.Receiver.Id, f.ReceiverDeputy.Id }.Order(), legacy.SelectedOfficerIds.Order());
    }

    [Fact]
    public async Task Deputies_are_optional_and_reassignment_retains_audit_and_previous_assignments()
    {
        await using var f = await Fixture.Create();
        await f.Endpoint.Save(f.Report.Id, f.Request(), default);
        await f.Endpoint.Save(f.Report.Id, f.Request() with { Controller = new(f.ControllerDeputy.Id, null) }, default);
        var data = await f.Get();
        Assert.Equal(f.ControllerDeputy.Id, data.Controller.PrimaryAssigneeId);
        Assert.Null(data.Controller.AlternateAssigneeId);
        Assert.Null(data.Receiver.AlternateAssigneeId);
        Assert.Null(data.FinalSubmission.AlternateAssigneeId);
        Assert.Equal(2, await f.Db.AuditEvents.CountAsync());
        Assert.Equal(4, await f.Db.ResponsibilityAssignments.CountAsync(x => x.EffectiveToUtc.HasValue));
    }

    [Theory]
    [InlineData("controller-missing")]
    [InlineData("receiver-missing")]
    [InlineData("submitter-missing")]
    [InlineData("same-deputy")]
    [InlineData("wrong-division")]
    [InlineData("inactive")]
    [InlineData("missing-division")]
    [InlineData("bad-version")]
    public async Task Invalid_pairs_reject_the_whole_save_without_partial_changes(string scenario)
    {
        await using var f = await Fixture.Create();
        var request = f.Request();
        switch (scenario)
        {
            case "controller-missing": request = request with { Controller = new(Guid.Empty, null) }; break;
            case "receiver-missing": request = request with { Receiver = new(Guid.Empty, null) }; break;
            case "submitter-missing": request = request with { FinalSubmission = new(Guid.Empty, null) }; break;
            case "same-deputy": request = request with { Receiver = new(f.Receiver.Id, f.Receiver.Id) }; break;
            case "wrong-division": request = request with { Controller = new(f.Submitter.Id, null) }; break;
            case "inactive": f.Receiver.IsActive = false; await f.Db.SaveChangesAsync(); break;
            case "missing-division": f.Report.ReceivingDivisionId = null; await f.Db.SaveChangesAsync(); request = request with { RowVersion = Convert.ToBase64String(f.Report.RowVersion) }; break;
            case "bad-version": request = request with { RowVersion = "bad-token" }; break;
        }
        await Assert.ThrowsAsync<TrackingValidationException>(() => f.Endpoint.Save(f.Report.Id, request, default));
        Assert.Single(f.Db.ResponsibilityAssignments);
        Assert.Empty(f.Db.AuditEvents);
        Assert.Equal(f.Submitter.Id, f.Task.PrimaryAssigneeId);
    }

    [Fact]
    public async Task Stale_edits_cannot_overwrite_an_assignment_change()
    {
        await using var f = await Fixture.Create();
        var stale = f.Request();
        await f.Endpoint.Save(f.Report.Id, stale, default);
        await Assert.ThrowsAsync<TrackingConflictException>(() => f.Endpoint.Save(f.Report.Id, stale, default));
        Assert.Single(f.Db.AuditEvents);
        Assert.Equal(3, await f.Db.ResponsibilityAssignments.CountAsync(x => !x.EffectiveToUtc.HasValue));
    }

    [Theory]
    [InlineData("controller", "ReportController")]
    [InlineData("receiver", "ComponentReceiver")]
    [InlineData("finalSubmission", "MainSender")]
    public async Task Unassign_deletes_only_the_selected_active_pair_and_preserves_history(string role, string type)
    {
        await using var f = await Fixture.Create();
        var request = f.Request() with
        {
            Controller = new(f.Controller.Id, f.ControllerDeputy.Id),
            Receiver = new(f.Receiver.Id, f.ReceiverDeputy.Id),
            FinalSubmission = new(f.Submitter.Id, f.SubmitterDeputy.Id)
        };
        await f.Endpoint.Save(f.Report.Id, request, default);
        var before = await f.Get();
        var active = await f.Db.ResponsibilityAssignments.SingleAsync(x => x.ResponsibilityType == type && !x.EffectiveToUtc.HasValue);
        var previous = await f.Db.ResponsibilityAssignments.SingleAsync(x => x.EffectiveToUtc.HasValue);
        var taskVersion = f.Task.RowVersion.ToArray();
        var officerCount = await f.Db.Users.CountAsync();

        await Assert.ThrowsAsync<TrackingConflictException>(() => f.Endpoint.Unassign(f.Report.Id, role, new(request.RowVersion), default));
        Assert.IsType<NoContentResult>(await f.Endpoint.Unassign(f.Report.Id, role, new(before.RowVersion), default));

        Assert.False(await f.Db.ResponsibilityAssignments.AnyAsync(x => x.Id == active.Id));
        Assert.True(await f.Db.ResponsibilityAssignments.AnyAsync(x => x.Id == previous.Id));
        Assert.Equal(2, await f.Db.ResponsibilityAssignments.CountAsync(x => !x.EffectiveToUtc.HasValue));
        var after = await f.Get();
        var cleared = role switch { "controller" => after.Controller, "receiver" => after.Receiver, _ => after.FinalSubmission };
        Assert.Null(cleared.PrimaryAssigneeId);
        Assert.Null(cleared.AlternateAssigneeId);
        Assert.NotEqual(before.RowVersion, after.RowVersion);
        Assert.Equal(f.Submitter.Id, f.Task.PrimaryAssigneeId);
        Assert.Equal(taskVersion, f.Task.RowVersion);
        Assert.Equal(officerCount, await f.Db.Users.CountAsync());
        var setup = new SetupController(f.Db, f.Access, new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>());
        var reportItems = Assert.IsAssignableFrom<IEnumerable<ReportItem>>(Assert.IsType<OkObjectResult>(await setup.Reports(default)).Value);
        Assert.Equal(new ReportDivisionLocks(role != "controller", role != "receiver", role != "finalSubmission"),
            Assert.Single(reportItems).DivisionLocks);
        var audit = await f.Db.AuditEvents.SingleAsync(x => x.Action == "ReportResponsibilityUnassigned");
        Assert.Contains(active.Id.ToString(), audit.DataJson);
        Assert.Equal(f.Manager.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task Unrelated_scopes_cannot_read_or_change_report_assignments()
    {
        await using var f = await Fixture.Create();
        var endpoint = new ReportResponsibilityAssignmentsController(f.Db, new TrackingAccess(f.Db, new Actor(f.Receiver.Id)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => endpoint.Get(f.Report.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => endpoint.Save(f.Report.Id, f.Request(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => endpoint.Unassign(f.Report.Id, "finalSubmission", new(f.Request().RowVersion), default));
        var grant = await f.Db.UserRoles.SingleAsync(x => x.UserId == f.Manager.Id);
        grant.ReportDefinitionId = Guid.NewGuid(); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Endpoint.Get(f.Report.Id, default));
        Assert.Empty(f.Db.AuditEvents);
    }

    [Fact]
    public async Task Larger_existing_receiving_teams_are_never_silently_removed()
    {
        await using var f = await Fixture.Create();
        foreach (var id in new[] { f.Receiver.Id, f.ReceiverDeputy.Id, Guid.NewGuid() })
            f.Db.ResponsibilityAssignments.Add(new ResponsibilityAssignment
            {
                ReportDefinitionId = f.Report.Id, DivisionId = f.ReceivingDivision.Id,
                ResponsibilityType = TrackingAccess.ComponentReceiverResponsibility, PrimaryAssigneeId = id,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1)
            });
        await f.Db.SaveChangesAsync();
        var data = await f.Get();
        Assert.Equal(3, data.ExistingReceiverCount);
        await Assert.ThrowsAsync<TrackingConflictException>(() => f.Endpoint.Save(f.Report.Id, f.Request(), default));
        await Assert.ThrowsAsync<TrackingConflictException>(() => f.Endpoint.Unassign(f.Report.Id, "receiver", new(data.RowVersion), default));
        Assert.Equal(4, await f.Db.ResponsibilityAssignments.CountAsync(x => !x.EffectiveToUtc.HasValue));
        Assert.Empty(f.Db.AuditEvents);
    }

    private sealed record Actor(Guid UserId) : ICurrentUser;
    private sealed class Fixture : IAsyncDisposable
    {
        public ReportTrackerDbContext Db { get; } = new(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Division ControllerDivision { get; } = new() { Code = "CTRL", Name = "Coordinating" };
        public Division ReceivingDivision { get; } = new() { Code = "RECV", Name = "Receiving" };
        public Division SubmittingDivision { get; } = new() { Code = "SUB", Name = "Submitting" };
        public AppUser Manager { get; private set; } = null!;
        public AppUser Controller { get; private set; } = null!;
        public AppUser ControllerDeputy { get; private set; } = null!;
        public AppUser Receiver { get; private set; } = null!;
        public AppUser ReceiverDeputy { get; private set; } = null!;
        public AppUser Submitter { get; private set; } = null!;
        public AppUser SubmitterDeputy { get; private set; } = null!;
        public ReportDefinition Report { get; private set; } = null!;
        public TrackingObligation Task { get; private set; } = null!;
        public ComponentDefinition Component { get; private set; } = null!;
        public TrackingAccess Access => new(Db, new Actor(Manager.Id));
        public ReportResponsibilityAssignmentsController Endpoint => new(Db, Access);
        public SaveReportResponsibilityAssignmentsRequest Request() => new(new(Controller.Id, null), new(Receiver.Id, null), new(Submitter.Id, null), Convert.ToBase64String(Report.RowVersion));
        public async Task<ReportResponsibilityAssignments> Get() => Assert.IsType<ReportResponsibilityAssignments>(Assert.IsType<OkObjectResult>((await Endpoint.Get(Report.Id, default)).Result).Value);

        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            AppUser Officer(string name, Division division) => new() { EmployeeId = name, DisplayName = name, Email = name + "@example.test", DivisionId = division.Id };
            f.Manager = Officer("Manager", f.ControllerDivision);
            f.Controller = Officer("Controller", f.ControllerDivision); f.ControllerDeputy = Officer("ControllerDeputy", f.ControllerDivision);
            f.Receiver = Officer("Receiver", f.ReceivingDivision); f.ReceiverDeputy = Officer("ReceiverDeputy", f.ReceivingDivision);
            f.Submitter = Officer("Submitter", f.SubmittingDivision); f.SubmitterDeputy = Officer("SubmitterDeputy", f.SubmittingDivision);
            f.Report = new ReportDefinition { Code = "R", Name = "Report", OwnerDivisionId = f.ControllerDivision.Id, ReceivingDivisionId = f.ReceivingDivision.Id, SubmittingDivisionId = f.SubmittingDivision.Id, BbRecipient = "BB" };
            var cycle = new ReportCycle { ReportDefinition = f.Report, ReportDefinitionId = f.Report.Id, PeriodKey = "2026-11", PeriodLabel = "November" };
            f.Task = new TrackingObligation { Name = "Final submission", Type = ObligationType.External, ReportCycle = cycle, ReportCycleId = cycle.Id, AccountableDivisionId = f.SubmittingDivision.Id, PrimaryAssigneeId = f.Submitter.Id, Recipient = "BB" };
            f.Component = new ComponentDefinition { Name = "Unchanged component", ProviderDivisionId = f.ControllerDivision.Id, ReceivingDivisionId = f.ReceivingDivision.Id, PrimaryAssigneeId = f.Controller.Id };
            var role = new AppRole { Name = "Manager" }; var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
            f.Db.AddRange(f.ControllerDivision, f.ReceivingDivision, f.SubmittingDivision, f.Manager, f.Controller, f.ControllerDeputy, f.Receiver, f.ReceiverDeputy, f.Submitter, f.SubmitterDeputy, f.Report, f.Task, f.Component, role, permission,
                new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
                new UserRole { UserId = f.Manager.Id, RoleId = role.Id, DivisionId = f.ControllerDivision.Id, ReportDefinitionId = f.Report.Id },
                new ResponsibilityAssignment { ReportDefinitionId = f.Report.Id, DivisionId = f.SubmittingDivision.Id, PrimaryAssigneeId = f.Submitter.Id, ResponsibilityType = "MainSender", EffectiveFromUtc = DateTime.UtcNow.AddDays(-1) });
            await f.Db.SaveChangesAsync(); await AccessControlSeed.PrepareAsync(f.Db, true); await f.Db.SaveChangesAsync(); return f;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

