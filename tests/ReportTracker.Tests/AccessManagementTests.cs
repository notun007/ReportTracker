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
public sealed class AccessManagementTests
{
    [Theory]
    [InlineData("")][InlineData("global")][InlineData("Division")][InlineData("Report")][InlineData("DivisionReport")]
    public async Task Missing_scope_details_never_grant_global_access(string scope)
    {
        await using var f=await Fixture.Create();
        await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(f.Roles["REPORT_PREPARER"].Id,scope)],default));
        Assert.Empty(f.Db.AuditEvents);
    }
    [Fact]
    public async Task Explicit_scopes_are_checked_and_administration_roles_require_global()
    {
        await using var f=await Fixture.Create(); var role=f.Roles["REPORT_PREPARER"].Id;
        foreach(var binding in new[]{new RoleAssignmentRequest(role,"Global"),new(role,"Division",f.Division.Id),new(role,"Report",null,f.Report.Id),new(role,"DivisionReport",f.Division.Id,f.Report.Id)})
            await f.Service.ValidateAssignmentsAsync([binding],default);
        await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(f.Roles["SYS_ADMIN"].Id,"Division",f.Division.Id)],default));
        await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(role,"Global",f.Division.Id)],default));
        await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(role,"Division",Guid.NewGuid())],default));
    }
    [Fact]
    public async Task Role_permission_alone_does_not_expose_unassigned_components_and_revocation_is_immediate()
    {
        await using var f=await Fixture.Create(); var access=new TrackingAccess(f.Db,new Actor(f.Member.Id));
        var grant=new UserRole {UserId=f.Member.Id,RoleId=f.Roles["REPORT_PREPARER"].Id,ReportDefinitionId=f.Report.Id};f.Db.Add(grant);await f.Db.SaveChangesAsync();
        Assert.Empty(await access.AuthorizedObligations().ToListAsync());
        f.Task.PrimaryAssigneeId=f.Member.Id;await f.Db.SaveChangesAsync();
        Assert.Single(await access.AuthorizedObligations().ToListAsync());Assert.Single(await access.AuthorizedObligations(true).ToListAsync());
        grant.IsActive=false;await f.Db.SaveChangesAsync();Assert.Empty(await access.AuthorizedObligations().ToListAsync());
        grant.IsActive=true;f.Roles["REPORT_PREPARER"].IsActive=false;await f.Db.SaveChangesAsync();Assert.Empty(await access.AuthorizedObligations().ToListAsync());
        f.Roles["REPORT_PREPARER"].IsActive=true;f.Member.IsActive=false;await f.Db.SaveChangesAsync();Assert.Empty(await access.AuthorizedObligations().ToListAsync());
    }
    [Fact]
    public async Task Access_replacement_retains_revoked_history_and_rejects_stale_edits()
    {
        await using var f=await Fixture.Create();var role=f.Roles["REPORT_PREPARER"].Id;
        var token=Convert.ToBase64String(f.Member.RowVersion);
        await f.Controller.SaveUserAccess(f.Member.Id,new([new(role,"Report",null,f.Report.Id)],token),default);
        await Assert.ThrowsAsync<TrackingConflictException>(()=>f.Controller.SaveUserAccess(f.Member.Id,new([],token),default));
        await f.Controller.SaveUserAccess(f.Member.Id,new([],Convert.ToBase64String(f.Member.RowVersion)),default);
        var historic=Assert.Single(await f.Db.UserRoles.Where(x=>x.UserId==f.Member.Id).ToListAsync());Assert.False(historic.IsActive);
        await f.Controller.SaveUserAccess(f.Member.Id,new([new(role,"Report",null,f.Report.Id)],Convert.ToBase64String(f.Member.RowVersion)),default);
        Assert.Equal(historic.Id,Assert.Single(await f.Db.UserRoles.Where(x=>x.UserId==f.Member.Id).ToListAsync()).Id);Assert.True(historic.IsActive);
        Assert.Equal(3,await f.Db.AuditEvents.CountAsync(x=>x.Action=="UserAccessChanged"));
    }
    [Fact]
    public async Task Last_access_administrator_cannot_remove_their_access_or_permission()
    {
        await using var f=await Fixture.Create();
        await Assert.ThrowsAsync<TrackingConflictException>(()=>f.Controller.SaveUserAccess(f.Admin.Id,new([],Convert.ToBase64String(f.Admin.RowVersion)),default));
        var role=f.Roles["SYS_ADMIN"];
        await Assert.ThrowsAsync<TrackingConflictException>(()=>f.Controller.UpdateRole(role.Id,new(role.Name,null,true,[AccessPermissions.Users],Convert.ToBase64String(role.RowVersion)),default));
        await Assert.ThrowsAsync<TrackingConflictException>(()=>f.Service.EnsureAdministratorRemainsAsync(default,deactivatedUserId:f.Admin.Id));
        Assert.Empty(f.Db.AuditEvents);
    }
    [Fact]
    public async Task Effective_permission_sources_are_combined_and_scopes_stay_separate()
    {
        await using var f=await Fixture.Create();
        await f.Controller.CreateRole(new("CUSTOM_PREP","Extra preparer",null,[AccessPermissions.ComponentRead]),default);
        var custom=await f.Db.Roles.SingleAsync(x=>x.Code=="CUSTOM_PREP");
        await f.Controller.SaveUserAccess(f.Member.Id,new([new(f.Roles["REPORT_PREPARER"].Id,"Report",null,f.Report.Id),new(custom.Id,"Report",null,f.Report.Id),new(custom.Id,"Division",f.Division.Id)],Convert.ToBase64String(f.Member.RowVersion)),default);
        var view=Assert.IsType<UserAccessDirectory>(Assert.IsType<OkObjectResult>((await f.Controller.UserAccess(f.Member.Id,default)).Result).Value);
        Assert.Equal(2,view.EffectivePermissions.Count(x=>x.Code==AccessPermissions.ComponentRead));
        Assert.Equal(2,view.EffectivePermissions.Single(x=>x.Code==AccessPermissions.ComponentRead&&x.Scope=="Report").Sources.Count);
    }
    [Fact]
    public async Task User_management_permission_cannot_grant_roles_and_system_admin_has_no_business_access()
    {
        await using var f=await Fixture.Create();
        Assert.False(await f.Access.HasAsync(AccessPermissions.Reports,f.Division.Id,f.Report.Id,default));
        await f.Controller.CreateRole(new("USER_MANAGER","User manager",null,[AccessPermissions.Users]),default);
        var role=await f.Db.Roles.SingleAsync(x=>x.Code=="USER_MANAGER");f.Db.UserRoles.Add(new(){UserId=f.Member.Id,RoleId=role.Id});await f.Db.SaveChangesAsync();
        var limited=new AccessManagementController(f.Db,new TrackingAccess(f.Db,new Actor(f.Member.Id)));
        Assert.IsType<OkObjectResult>((await limited.Catalog(default)).Result);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>limited.CreateRole(new("ESCALATED","Escalated",null,[AccessPermissions.Roles]),default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>limited.SaveUserAccess(f.Member.Id,new([],Convert.ToBase64String(f.Member.RowVersion)),default));
    }
    [Theory]
    [InlineData("unknown")][InlineData("duplicate")][InlineData("inactive")]
    public async Task Invalid_role_assignments_and_permissions_are_rejected(string scenario)
    {
        await using var f=await Fixture.Create();var role=f.Roles["REPORT_PREPARER"];
        if(scenario=="unknown")Assert.Throws<TrackingValidationException>(()=>AccessAdministration.ValidatePermissions(["everything.manage"]));
        else if(scenario=="duplicate")await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(role.Id,"Global"),new(role.Id,"Global")],default));
        else {role.IsActive=false;await f.Db.SaveChangesAsync();await Assert.ThrowsAsync<TrackingValidationException>(()=>f.Service.ValidateAssignmentsAsync([new(role.Id,"Global")],default));}
    }
    [Fact]
    public async Task Migration_preserves_old_role_ids_scopes_and_operational_assignments()
    {
        await using var f=await Fixture.Create();
        var legacy=new AppRole{Name="Report Coordinator"};var grant=new UserRole{UserId=f.Member.Id,RoleId=legacy.Id,DivisionId=f.Division.Id,ReportDefinitionId=f.Report.Id};
        f.Db.Roles.Remove(f.Roles["REPORT_ADMIN"]);f.Db.AddRange(legacy,grant,new RolePermission{RoleId=legacy.Id,PermissionId=(await f.Db.Permissions.SingleAsync(x=>x.Code==AccessPermissions.Reports)).Id});f.Task.PrimaryAssigneeId=f.Member.Id;
        await f.Db.SaveChangesAsync();var version=f.Task.RowVersion.ToArray();
        await AccessControlSeed.PrepareAsync(f.Db,true);await f.Db.SaveChangesAsync();
        Assert.Equal(legacy.Id,(await f.Db.Roles.SingleAsync(x=>x.Code=="REPORT_ADMIN")).Id);Assert.Equal("Reporting Administrator",legacy.Name);
        Assert.Equal(f.Division.Id,grant.DivisionId);Assert.Equal(f.Report.Id,grant.ReportDefinitionId);Assert.Equal(version,f.Task.RowVersion);
        var access=new TrackingAccess(f.Db,new Actor(f.Member.Id));Assert.True(await access.HasAsync(AccessPermissions.Cycles,f.Division.Id,f.Report.Id,default));Assert.False(await access.HasAsync(AccessPermissions.Cycles,f.Division.Id,Guid.NewGuid(),default));
        Assert.Contains(await f.Db.UserRoles.ToListAsync(),x=>x.UserId==f.Member.Id&&x.RoleId==f.Roles["REPORT_PREPARER"].Id&&x.ReportDefinitionId==f.Report.Id);
    }
    [Fact]
    public async Task Granular_cycle_role_can_load_configuration_reference_data_without_report_edit_permission()
    {
        await using var f=await Fixture.Create();
        await f.Controller.CreateRole(new("CYCLE_MANAGER","Cycle manager",null,[AccessPermissions.Cycles]),default);
        var role=await f.Db.Roles.SingleAsync(x=>x.Code=="CYCLE_MANAGER");
        f.Db.UserRoles.Add(new(){UserId=f.Member.Id,RoleId=role.Id,ReportDefinitionId=f.Report.Id});await f.Db.SaveChangesAsync();
        var access=new TrackingAccess(f.Db,new Actor(f.Member.Id));
        Assert.False(await access.HasAsync(AccessPermissions.Reports,f.Division.Id,f.Report.Id,default));
        Assert.IsType<OkObjectResult>((await new ReportingFrequenciesController(f.Db,access).List(default)).Result);
        Assert.IsType<OkObjectResult>((await new SubmissionRecipientsController(f.Db,access).List(default)).Result);
    }
    [Fact]
    public async Task Reminder_policy_visibility_respects_report_scope()
    {
        await using var f=await Fixture.Create();
        var other=new ReportDefinition{Code="OTHER",Name="Other report",OwnerDivisionId=f.Division.Id,BbRecipient="BB"};
        f.Db.AddRange(other,new ReminderPolicy{Name="Visible",ReportDefinitionId=f.Report.Id,ObligationCategory="All",ChannelsCsv="Email"},new ReminderPolicy{Name="Hidden",ReportDefinitionId=other.Id,ObligationCategory="All",ChannelsCsv="Email"},new ReminderPolicy{Name="Global",ObligationCategory="All",ChannelsCsv="Email"});
        f.Db.UserRoles.Add(new(){UserId=f.Member.Id,RoleId=f.Roles["REPORT_ADMIN"].Id,ReportDefinitionId=f.Report.Id});await f.Db.SaveChangesAsync();
        var controller=new RemindersController(f.Db,new TrackingAccess(f.Db,new Actor(f.Member.Id)),null!);
        var rows=Assert.IsAssignableFrom<IEnumerable<ReminderPolicyItem>>(Assert.IsType<OkObjectResult>((await controller.Policies(default)).Result).Value);
        Assert.Equal("Visible",Assert.Single(rows).Name);
    }
    [Fact]
    public async Task Permission_only_role_changes_advance_the_concurrency_token()
    {
        await using var f=await Fixture.Create();var role=f.Roles["REPORT_PREPARER"];var token=Convert.ToBase64String(role.RowVersion);
        var request=new UpdateAccessRoleRequest(role.Name,role.Description,true,[AccessPermissions.ComponentRead],token);
        await f.Controller.UpdateRole(role.Id,request,default);
        Assert.NotEqual(token,Convert.ToBase64String(role.RowVersion));
        await Assert.ThrowsAsync<TrackingConflictException>(()=>f.Controller.UpdateRole(role.Id,request,default));
    }
    private sealed record Actor(Guid UserId):ICurrentUser;
    private sealed class Fixture:IAsyncDisposable
    {
        public ReportTrackerDbContext Db {get;}=new(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Division Division=new(){Code="D",Name="Division"};public AppUser Admin=null!,Member=null!;public ReportDefinition Report=null!;public TrackingObligation Task=null!;
        public IReadOnlyDictionary<string,AppRole> Roles=null!;
        public TrackingAccess Access=>new(Db,new Actor(Admin.Id));public AccessAdministration Service=>new(Db,Access);public AccessManagementController Controller=>new(Db,Access);
        public static async Task<Fixture> Create(){var f=new Fixture();f.Admin=new(){EmployeeId="ADMIN",DisplayName="Admin",Email="admin@example.test",DivisionId=f.Division.Id};f.Member=new(){EmployeeId="MEMBER",DisplayName="Member",Email="member@example.test",DivisionId=f.Division.Id};f.Report=new(){Code="R",Name="Report",OwnerDivisionId=f.Division.Id,BbRecipient="BB"};var cycle=new ReportCycle{ReportDefinition=f.Report,PeriodKey="2026-11",PeriodLabel="November"};f.Task=new(){ReportCycle=cycle,Name="Component",Recipient="Receiver",AccountableDivisionId=f.Division.Id,Type=ObligationType.Internal};f.Db.AddRange(f.Division,f.Admin,f.Member,f.Task);await f.Db.SaveChangesAsync();f.Roles=await AccessControlSeed.PrepareAsync(f.Db,false);f.Db.UserRoles.Add(new(){UserId=f.Admin.Id,RoleId=f.Roles["SYS_ADMIN"].Id});await f.Db.SaveChangesAsync();return f;}
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
