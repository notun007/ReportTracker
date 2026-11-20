using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class DivisionResponsibilityTests
{
    [Fact]
    public async Task Report_can_be_created_without_officers_and_assigned_later_for_cycle_generation()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        (await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id)).ReportDefinitionId = null;
        await db.SaveChangesAsync();
        var request = new CreateReportRequest("LATER", "Assign officers later", null, f.C.Id, "BB", null, "Monthly",
            SubmittingDivisionId: f.D.Id, ReceivingDivisionId: f.C.Id, ReportDeadlineOffsetDays: 2);
        Assert.IsType<CreatedResult>(await Controller(db, f.Coordinator.Id).CreateReport(request, default));
        var report = await db.ReportDefinitions.SingleAsync(x => x.Code == "LATER");
        Assert.False(await db.ResponsibilityAssignments.AnyAsync(x => x.ReportDefinitionId == report.Id));
        var generator = scope.ServiceProvider.GetRequiredService<ICycleGenerationService>();
        var pending = await generator.GenerateAsync(Today(), default);
        Assert.Contains(pending.Warnings, x => x.Contains("LATER") && x.Contains("Responsibilities"));
        Assert.False(await db.ReportCycles.AnyAsync(x => x.ReportDefinitionId == report.Id));

        var endpoint = new ReportResponsibilityAssignmentsController(db, new TrackingAccess(db, new Actor(f.Coordinator.Id)));
        var directory = Assert.IsType<ReportResponsibilityAssignments>(Assert.IsType<OkObjectResult>((await endpoint.Get(report.Id, default)).Result).Value);
        Assert.Null(directory.FinalSubmission.PrimaryAssigneeId);
        Assert.Equal(f.D.Id, directory.FinalSubmission.DivisionId);
        await endpoint.Save(report.Id, new(new(f.Coordinator.Id, null), new(f.Coordinator.Id, null),
            new(f.Submitter.Id, null), directory.RowVersion), default);
        await generator.GenerateAsync(Today(), default);
        var cycles = await db.ReportCycles.Include(x => x.Obligations).Where(x => x.ReportDefinitionId == report.Id).ToListAsync();
        Assert.NotEmpty(cycles);
        Assert.All(cycles, cycle => Assert.Equal(f.Submitter.Id, Assert.Single(cycle.Obligations).PrimaryAssigneeId));
        var version = await db.DefinitionVersions.SingleAsync(x => x.ReportDefinitionId == report.Id);
        Assert.Equal(2, (await db.ScheduleRules.SingleAsync(x => x.DefinitionVersionId == version.Id)).OffsetDays);
    }

    [Fact]
    public async Task Unassigned_report_still_requires_active_divisions_and_rejects_a_deputy_without_primary()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        (await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id)).ReportDefinitionId = null;
        await db.SaveChangesAsync();
        var request = new CreateReportRequest("INVALID", "Invalid report", null, f.C.Id, "BB", null, "Monthly",
            SubmittingDivisionId: f.D.Id, ReceivingDivisionId: f.C.Id);
        var controller = Controller(db, f.Coordinator.Id);
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.CreateReport(request with { SubmittingDivisionId = null }, default));
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.CreateReport(request with { ReceivingDivisionId = null }, default));
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.CreateReport(request with { AlternateSubmittingAssigneeId = f.Submitter.Id }, default));
        Assert.False(await db.ReportDefinitions.AnyAsync(x => x.Code == "INVALID"));
    }

    [Fact]
    public async Task New_components_are_required_and_historical_optional_obligations_do_not_block_readiness()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>().GenerateAsync(Today(), default);
        var required = await db.TrackingObligations.SingleAsync(x => x.Name == "Component A");
        var optional = await db.TrackingObligations.SingleAsync(x => x.Name == "Component B");
        Assert.True(optional.IsRequired); Assert.Equal(ReceiptStatus.NotRequired, optional.ReceiptStatus); Assert.Equal(EvidenceStatus.NotRequired, optional.EvidenceStatus); optional.IsRequired = false; required.SendingStatus = SendingStatus.Sent; required.EvidenceStatus = EvidenceStatus.NotRequired; required.ReceiptStatus = ReceiptStatus.NotRequired;
        await db.SaveChangesAsync();
        var response = await new ReadinessController(db, new TrackingAccess(db, new Actor(f.Coordinator.Id))).Get(required.ReportCycleId, default);
        var result = Assert.IsType<SubmissionReadinessDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(1, result.RequiredComponents); Assert.Equal(1, result.CompletedComponents); Assert.True(result.IsReady);
        Assert.False(Assert.Single(result.Components, x => x.Name == "Component B").IsBlocking);
    }

    [Fact]
    public async Task Creating_report_persists_separate_coordinator_and_explicit_final_sender()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        (await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id)).ReportDefinitionId = null;
        await db.SaveChangesAsync();
        var request = new CreateReportRequest("NEW", "New report", null, f.C.Id, "BB", null, "Monthly",
            SubmittingDivisionId: f.D.Id, PrimarySubmittingAssigneeId: f.Submitter.Id, ReceivingDivisionId: f.C.Id, ReportTypeId: 1);
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            Controller(db, f.Coordinator.Id).CreateReport(request with { ReportTypeId = 3 }, default));
        Assert.IsType<CreatedResult>(await Controller(db, f.Coordinator.Id).CreateReport(request, default));
        var report = await db.ReportDefinitions.SingleAsync(x => x.Code == "NEW");
        Assert.Equal(f.C.Id, report.OwnerDivisionId); Assert.Equal(f.D.Id, report.SubmittingDivisionId); Assert.Equal(1, report.ReportTypeId);
        var assignment = await db.ResponsibilityAssignments.SingleAsync(x => x.ReportDefinitionId == report.Id);
        Assert.Equal(f.D.Id, assignment.DivisionId); Assert.Equal(f.Submitter.Id, assignment.PrimaryAssigneeId);
    }

    [Fact]
    public async Task Automatic_cycles_use_A_B_for_preparation_C_for_receiving_and_D_for_BB()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var generator = scope.ServiceProvider.GetRequiredService<ICycleGenerationService>();
        var result = await generator.GenerateAsync(Today().AddDays(60), default);
        Assert.True(result.CyclesCreated > 0); Assert.Empty(result.Warnings);
        var cycles = await db.ReportCycles.Include(x => x.Obligations).ToListAsync();
        foreach (var cycle in cycles)
        {
            var main = Assert.Single(cycle.Obligations, x => x.Type == ObligationType.External);
            Assert.Equal(f.D.Id, main.AccountableDivisionId); Assert.Equal(f.Submitter.Id, main.PrimaryAssigneeId);
            var components = cycle.Obligations.Where(x => x.Type == ObligationType.Internal).ToList();
            Assert.Equal(new[] { f.A.Id, f.B.Id }.Order(), components.Select(x => x.AccountableDivisionId).Order());
            Assert.All(components, x => { Assert.Equal(f.C.Id, x.ReceivingDivisionId); Assert.Equal(f.C.Name, x.Recipient); Assert.Equal(main.Id, x.ParentObligationId); });
        }
        Assert.Equal(f.C.Id, f.Report.OwnerDivisionId);
        Assert.Equal(0, (await generator.GenerateAsync(Today().AddDays(60), default)).CyclesCreated);
    }

    [Fact]
    public async Task Yearly_monthly_generation_creates_all_twelve_calendar_cycles_without_a_saved_preset()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var generator = scope.ServiceProvider.GetRequiredService<ICycleGenerationService>();
        var year = Today().Year;
        var version = await db.DefinitionVersions.SingleAsync(x => x.ReportDefinitionId == f.Report.Id);
        version.ComponentDeadlineOffsetDays = 1;
        db.ScheduleRules.Add(new ScheduleRule { DefinitionVersionId = version.Id, PeriodConvention = "PeriodEndPlusCalendarDays", OffsetDays = 2 });
        await db.SaveChangesAsync();

        var preview = await generator.PreviewYearAsync(year, f.Report.Id, default);

        Assert.Equal(12, preview.TotalCycles);
        Assert.Equal(12, preview.NewCycles);
        Assert.Equal(0, preview.SkippedCycles);
        Assert.Equal(new DateOnly(year, 1, 1), preview.Items.First().PeriodStart);
        Assert.Equal(new DateOnly(year, 12, 31), preview.Items.Last().PeriodEnd);
        Assert.Equal(new DateOnly(year, 2, 2), preview.Items.First().FinalDeadline);
        Assert.Equal(new DateOnly(year, 2, 1), preview.Items.First().DefaultComponentDeadline);
        Assert.Equal(2, preview.Items.First().ComponentDeadlines!.Count);
        Assert.All(preview.Items.First().ComponentDeadlines!, component =>
            Assert.Equal(new DateOnly(year, 2, 1), component.Deadline));

        var selected = preview.Items.Take(3).Select(x => x.PeriodKey).ToArray();
        var result = await generator.GenerateYearAsync(year, f.Report.Id, selected, default);

        Assert.Equal(3, result.CyclesCreated);
        Assert.Equal(3, await db.ReportCycles.CountAsync());
        Assert.Equal(0, result.SkippedCycles);
        var january = await db.ReportCycles.Include(x => x.Obligations).SingleAsync(x => x.PeriodKey == $"{year}-01");
        var main = Assert.Single(january.Obligations, x => x.Type == ObligationType.External);
        Assert.Equal(new DateTime(year, 2, 2, 11, 0, 0, DateTimeKind.Utc), main.OriginalDueAtUtc);
        Assert.All(january.Obligations.Where(x => x.Type == ObligationType.Internal),
            component => Assert.Equal(main.OriginalDueAtUtc.AddDays(-1), component.OriginalDueAtUtc));

        (await db.ScheduleRules.SingleAsync(x => x.DefinitionVersionId == version.Id)).OffsetDays = 5;
        await db.SaveChangesAsync();

        var remaining = await generator.PreviewYearAsync(year, f.Report.Id, default);
        Assert.Equal(3, remaining.ExistingCycles);
        Assert.Equal(9, remaining.NewCycles);
        Assert.Equal(new DateOnly(year, 2, 2), remaining.Items.Single(x => x.PeriodKey == $"{year}-01").FinalDeadline);
        Assert.Equal(new DateOnly(year, 5, 5), remaining.Items.Single(x => x.PeriodKey == $"{year}-04").FinalDeadline);
        var completed = await generator.GenerateYearAsync(year, f.Report.Id,
            remaining.Items.Where(x => x.Status == "New").Select(x => x.PeriodKey).ToArray(), default);
        Assert.Equal(9, completed.CyclesCreated);
        Assert.Equal(12, await db.ReportCycles.CountAsync());
    }

    [Fact]
    public async Task Yearly_preview_always_shows_the_default_component_deadline_when_no_components_exist()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var version = await db.DefinitionVersions.SingleAsync(x => x.ReportDefinitionId == f.Report.Id);
        version.ComponentDeadlineOffsetDays = 4;
        db.ComponentDefinitions.RemoveRange(await db.ComponentDefinitions.Where(x => x.DefinitionVersionId == version.Id).ToListAsync());
        db.ScheduleRules.Add(new ScheduleRule { DefinitionVersionId = version.Id, PeriodConvention = "PeriodEndPlusCalendarDays", OffsetDays = 10 });
        await db.SaveChangesAsync();

        var preview = await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>()
            .PreviewYearAsync(Today().Year, f.Report.Id, default);
        var january = preview.Items.Single(x => x.PeriodKey == $"{Today().Year}-01");

        Assert.Equal(new DateOnly(Today().Year, 2, 10), january.FinalDeadline);
        Assert.Equal(new DateOnly(Today().Year, 2, 6), january.DefaultComponentDeadline);
        Assert.Empty(january.ComponentDeadlines!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generator_skips_missing_or_wrong_division_assignments_instead_of_choosing_an_arbitrary_officer(bool mismatch)
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var assignment = await db.ResponsibilityAssignments.SingleAsync();
        if (mismatch) assignment.DivisionId = f.C.Id; else db.Remove(assignment);
        await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>().GenerateAsync(Today().AddDays(30), default);
        Assert.Equal(0, result.CyclesCreated); Assert.Single(result.Warnings); Assert.Empty(await db.ReportCycles.ToListAsync());
    }

    [Fact]
    public async Task Manual_cycles_match_the_configured_submitting_division()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var controller = Controller(db, f.Coordinator.Id);
        var year = Today().Year; var januaryStart = new DateOnly(year, 1, 1); var januaryEnd = new DateOnly(year, 1, 31);
        CreateCycleRequest Request(Guid division, Guid officer, string key = "manual", DateOnly? start = null, DateOnly? end = null, int? reportingYear = null) =>
            new(key, "Manual cycle", start ?? januaryStart, end ?? januaryEnd,
                [new CreateObligationRequest("BB", "BB", division, officer, null, "BB", DateTime.UtcNow.AddDays(1), false, true)], reportingYear ?? year);
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.CreateCycle(f.Report.Id, Request(f.C.Id, f.Coordinator.Id), default));
        Assert.Empty(await db.ReportCycles.ToListAsync());
        Assert.IsType<CreatedResult>(await controller.CreateCycle(f.Report.Id, Request(f.D.Id, f.Submitter.Id), default));
        Assert.Equal(f.D.Id, (await db.TrackingObligations.SingleAsync()).AccountableDivisionId);
        await Assert.ThrowsAsync<TrackingConflictException>(() =>
            controller.CreateCycle(f.Report.Id, Request(f.D.Id, f.Submitter.Id), default));
        var duplicateStart = await Assert.ThrowsAsync<TrackingConflictException>(() => controller.CreateCycle(f.Report.Id,
            Request(f.D.Id, f.Submitter.Id, "manual-start", januaryStart, new DateOnly(year, 2, 1)), default));
        Assert.Contains("starts", duplicateStart.Message);
        var duplicateEnd = await Assert.ThrowsAsync<TrackingConflictException>(() => controller.CreateCycle(f.Report.Id,
            Request(f.D.Id, f.Submitter.Id, "manual-end", new DateOnly(year, 1, 2), januaryEnd), default));
        Assert.Contains("ends", duplicateEnd.Message);
        var overlap = await Assert.ThrowsAsync<TrackingConflictException>(() => controller.CreateCycle(f.Report.Id,
            Request(f.D.Id, f.Submitter.Id, "manual-overlap", new DateOnly(year, 1, 15), new DateOnly(year, 2, 15)), default));
        Assert.Contains("overlaps", overlap.Message);
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.CreateCycle(f.Report.Id,
            Request(f.D.Id, f.Submitter.Id, "wrong-year", januaryStart, januaryEnd, year + 1), default));
        Assert.Single(await db.ReportCycles.ToListAsync());
    }

    [Fact]
    public async Task Responsibility_changes_preserve_existing_cycles_and_reject_officers_from_another_division()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>().GenerateAsync(Today(), default);
        var existing = await db.TrackingObligations.Where(x => x.Type == ObligationType.External).SingleAsync();
        var controller = Controller(db, f.Coordinator.Id);
        await Assert.ThrowsAsync<TrackingValidationException>(() => controller.SetResponsibilities(f.Report.Id,
            new(f.C.Id, f.D.Id, f.Coordinator.Id, null), default));
        Assert.IsType<NoContentResult>(await controller.SetResponsibilities(f.Report.Id, new(f.C.Id, f.C.Id, f.Coordinator.Id, null), default));
        Assert.Equal(f.D.Id, existing.AccountableDivisionId); Assert.Equal(f.Submitter.Id, existing.PrimaryAssigneeId);
        Assert.Equal(f.C.Id, f.Report.SubmittingDivisionId);
        Assert.Single(await db.ResponsibilityAssignments.Where(x => !x.EffectiveToUtc.HasValue).ToListAsync());
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "ReportResponsibilitiesChanged"));
    }

    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrackingRules.Dhaka));

    [Fact]
    public async Task Component_contributors_are_snapshotted_and_only_the_lead_gets_handover_access()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        // This test exercises the unchanged contributor rules as an explicit global manager.
        var grant = await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id);
        grant.DivisionId = null; grant.ReportDefinitionId = null;
        var component = await db.ComponentDefinitions.SingleAsync(x => x.Name == "Component A");
        var lead = component.PrimaryAssigneeId!.Value;
        var reviewer = new AppUser { EmployeeId = "reviewer", DisplayName = "Reviewer", Email = "reviewer@example.test", DivisionId = f.A.Id };
        var preparer = new AppUser { EmployeeId = "preparer", DisplayName = "Preparer", Email = "preparer@example.test", DivisionId = f.A.Id };
        db.Users.AddRange(reviewer, preparer);
        await db.SaveChangesAsync();
        var controller = new ComponentsController(db, new TrackingAccess(db, new Actor(f.Coordinator.Id)));
        var contributors = new ComponentContributorItem[] { new(lead, "LeadPreparer"), new(lead, "Preparer"),
            new(preparer.Id, "Preparer"), new(reviewer.Id, "Reviewer"), new(reviewer.Id, "BackupPreparer") };
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            controller.SetContributors(f.Report.Id, component.Id, new([new(lead, "LeadPreparer"), new(f.Submitter.Id, "Reviewer")]), default));
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            controller.SetContributors(f.Report.Id, component.Id, new([new(lead, "LeadPreparer"), new(lead, "Reviewer"), new(lead, "Reviewer")]), default));
        var saved = await controller.SetContributors(f.Report.Id, component.Id, new(contributors), default);
        Assert.Equal(contributors, Assert.IsType<ComponentItem>(Assert.IsType<OkObjectResult>(saved.Result).Value).Contributors);

        await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>().GenerateAsync(Today(), default);
        var task = await db.TrackingObligations.SingleAsync(x => x.Name == "Component A");
        Assert.Equal(contributors, ComponentValidation.Contributors(task.ContributorsJson, task.PrimaryAssigneeId, task.AlternateAssigneeId));
        Assert.True(await new TrackingAccess(db, new Actor(lead)).AuthorizedObligations(true).AnyAsync(x => x.Id == task.Id));
        Assert.False(await new TrackingAccess(db, new Actor(preparer.Id)).AuthorizedObligations(true).AnyAsync(x => x.Id == task.Id));
        Assert.False(await new TrackingAccess(db, new Actor(reviewer.Id)).AuthorizedObligations(true).AnyAsync(x => x.Id == task.Id));
        await controller.SetContributors(f.Report.Id, component.Id, new([new(lead, "LeadPreparer")]), default);
        Assert.Equal(contributors, ComponentValidation.Contributors(task.ContributorsJson, task.PrimaryAssigneeId, task.AlternateAssigneeId));
    }

    [Fact]
    public async Task Report_destinations_accept_multiple_departments_of_one_recipient_and_snapshot_new_cycles()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var recipient = new SubmissionRecipient { Name = "External Audit Office" };
        var first = new SubmissionRecipientDepartment { Name = "Audit", SubmissionRecipientId = recipient.Id };
        var second = new SubmissionRecipientDepartment { Name = "Supervision", SubmissionRecipientId = recipient.Id };
        var other = new SubmissionRecipient { Name = "Other organization" };
        var unrelated = new SubmissionRecipientDepartment { Name = "Audit", SubmissionRecipientId = other.Id };
        db.AddRange(recipient, first, second, other, unrelated);
        (await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id)).ReportDefinitionId = null;
        await db.SaveChangesAsync();
        var controller = Controller(db, f.Coordinator.Id);
        var request = new CreateReportRequest("AUDIT", "Audit Report", "Monthly audit summary", f.C.Id, null, "AUDIT-2026", "Monthly",
            DueTime: new TimeOnly(16, 30), ComponentDeadlineOffsetDays: 0,
            SubmittingDivisionId: f.D.Id, PrimarySubmittingAssigneeId: f.Submitter.Id, ReceivingDivisionId: f.C.Id,
            SubmissionRecipientId: recipient.Id, SubmissionDepartmentIds: [first.Id, second.Id]);
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            controller.CreateReport(request with { SubmissionDepartmentIds = [first.Id, unrelated.Id] }, default));
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            controller.CreateReport(request with { SubmissionDepartmentIds = [first.Id, first.Id] }, default));
        await Assert.ThrowsAsync<TrackingValidationException>(() =>
            controller.CreateReport(request with { SubmissionDepartmentIds = [] }, default));
        Assert.False(await db.ReportDefinitions.AnyAsync(x => x.Code == "AUDIT"));
        Assert.IsType<CreatedResult>(await controller.CreateReport(request, default));
        var report = await db.ReportDefinitions.SingleAsync(x => x.Code == "AUDIT");
        var items = Assert.IsAssignableFrom<IEnumerable<ReportItem>>(Assert.IsType<OkObjectResult>(await controller.Reports(default)).Value);
        var item = Assert.Single(items, x => x.Id == report.Id);
        Assert.Equal(request.DueTime, item.DueTime);
        Assert.Equal(request.ComponentDeadlineOffsetDays, item.ComponentDeadlineOffsetDays);
        Assert.Equal("Asia/Dhaka", item.TimeZoneId);
        Assert.Equal(request.InstructionReference, item.InstructionReference);
        Assert.Equal(request.Description, item.Description);
        Assert.Equal(recipient.Id, item.SubmissionRecipientId);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), item.SubmissionDepartmentIds!.Order());
        Assert.Equal("External Audit Office — Audit; Supervision", item.SubmissionDestination);

        await scope.ServiceProvider.GetRequiredService<ICycleGenerationService>().GenerateAsync(Today(), default);
        var automatic = await db.TrackingObligations.SingleAsync(x => x.ReportCycle.ReportDefinitionId == report.Id);
        Assert.Equal(item.SubmissionDestination, automatic.Recipient);
        Assert.IsType<NoContentResult>(await controller.SetSubmissionDestination(report.Id, new(other.Id, [unrelated.Id]), default));
        var manualDate = new DateOnly(Today().Year + 1, 1, 1);
        var manual = new CreateCycleRequest("new-destination", "New destination", manualDate, manualDate,
            [new CreateObligationRequest("Final submission", "BB", f.D.Id, f.Submitter.Id, null, "Client value", DateTime.UtcNow.AddDays(1), false, true)], manualDate.Year);
        Assert.IsType<CreatedResult>(await controller.CreateCycle(report.Id, manual, default));
        var saved = await db.TrackingObligations.SingleAsync(x => x.ReportCycle.PeriodKey == "new-destination");
        Assert.Equal("Other organization — Audit", saved.Recipient);
        Assert.Equal("External Audit Office — Audit; Supervision", automatic.Recipient);
        Assert.Equal(unrelated.Id, (await db.ReportSubmissionDepartments.SingleAsync(x => x.ReportDefinitionId == report.Id)).DepartmentId);
    }

    [Fact]
    public async Task Recipient_directory_is_readable_by_report_managers_but_only_admins_can_add_entries()
    {
        using var provider = Provider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>(); var f = await Seed(db);
        var directory = new SubmissionRecipientsController(db, new TrackingAccess(db, new Actor(f.Coordinator.Id)));
        Assert.IsType<OkObjectResult>((await directory.List(default)).Result);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => directory.Create(new("External auditor"), default));
        var role = new AppRole { Name = "Admin" };
        var permission = new Permission { Code = "administration.manage", Name = "Manage administration" };
        db.AddRange(role, permission, new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
            new UserRole { RoleId = role.Id, UserId = f.Coordinator.Id });
        await db.SaveChangesAsync();
        await directory.Create(new(" External auditor "), default);
        var recipient = await db.SubmissionRecipients.SingleAsync();
        Assert.Equal("External auditor", recipient.Name);
        await Assert.ThrowsAsync<TrackingConflictException>(() => directory.Create(new("external auditor"), default));
        await directory.CreateDepartment(recipient.Id, new("Financial audit"), default);
        await Assert.ThrowsAsync<TrackingConflictException>(() => directory.CreateDepartment(recipient.Id, new("financial audit"), default));
        await Assert.ThrowsAsync<TrackingConflictException>(() =>
            directory.CreateDepartments(recipient.Id, new(["Supervision", "financial audit"]), default));
        Assert.Single(await db.SubmissionRecipientDepartments.ToListAsync());
        var batch = Assert.IsType<ObjectResult>(await directory.CreateDepartments(recipient.Id, new(["Supervision", " Risk "]), default));
        Assert.Equal(new[] { "Supervision", "Risk" }, Assert.IsAssignableFrom<IEnumerable<LookupItem>>(batch.Value).Select(x => x.Name));
        var rows = Assert.IsAssignableFrom<IEnumerable<SubmissionRecipientItem>>(Assert.IsType<OkObjectResult>((await directory.List(default)).Result).Value);
        Assert.Equal(new[] { "Financial audit", "Risk", "Supervision" }, Assert.Single(rows).Departments.Select(x => x.Name));
        Assert.Equal(4, await db.AuditEvents.CountAsync(x => x.Action.StartsWith("SubmissionRecipient")));
    }

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:Oracle"] = "User Id=unused;Password=unused;Data Source=unused",
            ["Automation:Enabled"] = "false"
        }).Build());
        services.RemoveAll<DbContextOptions<ReportTrackerDbContext>>();
        services.AddSingleton(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return services.BuildServiceProvider();
    }
    private static SetupController Controller(ReportTrackerDbContext db, Guid actor) => new(db, new TrackingAccess(db, new Actor(actor)), new PasswordHasher<AppUser>());
    private sealed record Actor(Guid UserId) : ICurrentUser;
    private sealed record Fixture(Division A, Division B, Division C, Division D, ReportDefinition Report, AppUser Coordinator, AppUser Submitter);
    private static async Task<Fixture> Seed(ReportTrackerDbContext db)
    {
        Division Division(string code) => new() { Code = code, Name = "Division " + code };
        var a = Division("A"); var b = Division("B"); var c = Division("C"); var d = Division("D");
        AppUser User(string name, Division division) => new() { EmployeeId = name, DisplayName = name, Email = name + "@example.test", DivisionId = division.Id };
        var preparerA = User("PreparerA", a); var preparerB = User("PreparerB", b); var coordinator = User("Coordinator", c); var submitter = User("Submitter", d);
        var report = new ReportDefinition { Code = "RIT", Name = "RIT", OwnerDivisionId = c.Id, SubmittingDivisionId = d.Id, BbRecipient = "BB" };
        var version = new DefinitionVersion { ReportDefinitionId = report.Id, VersionNumber = 1, EffectiveFrom = Today().AddDays(-1), Frequency = Frequency.Monthly, DueTime = new TimeOnly(17, 0) };
        report.Versions.Add(version);
        var role = new AppRole { Name = "Coordinator" }; var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
        db.AddRange(a, b, c, d, preparerA, preparerB, coordinator, submitter, report, role, permission,
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id }, new UserRole { UserId = coordinator.Id, RoleId = role.Id, DivisionId = c.Id, ReportDefinitionId = report.Id },
            new ResponsibilityAssignment { ReportDefinitionId = report.Id, DivisionId = d.Id, PrimaryAssigneeId = submitter.Id, ResponsibilityType = "MainSender", EffectiveFromUtc = DateTime.UtcNow.AddDays(-1) },
            new ComponentDefinition { DefinitionVersionId = version.Id, Name = "Component A", ProviderDivisionId = a.Id, ReceivingDivisionId = c.Id, PrimaryAssigneeId = preparerA.Id },
            new ComponentDefinition { DefinitionVersionId = version.Id, Name = "Component B", ProviderDivisionId = b.Id, ReceivingDivisionId = c.Id, PrimaryAssigneeId = preparerB.Id });
        await db.SaveChangesAsync(); await AccessControlSeed.PrepareAsync(db, true); await db.SaveChangesAsync(); return new(a, b, c, d, report, coordinator, submitter);
    }
}
