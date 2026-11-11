using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class SubmissionResponsibilityTests
{
    [Fact]
    public async Task Receiving_officer_can_read_task_but_cannot_send_or_upload_evidence()
    {
        await using var db = Db(); var f = await Seed(db);
        var access = Access(db, f.Receiver.Id);
        Assert.True(await access.AuthorizedObligations().AnyAsync(x => x.Id == f.Task.Id));
        Assert.False(await access.AuthorizedObligations(true).AnyAsync(x => x.Id == f.Task.Id));
        Assert.False(await Access(db, f.Stranger.Id).AuthorizedObligations().AnyAsync(x => x.Id == f.Task.Id));
        var result = await Controller(db, f.Receiver.Id).UploadEvidence(f.Task.Id, new FormFile(Stream.Null, 0, 0, "file", "proof.pdf"), default);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Sender_cannot_confirm_own_receipt_even_when_also_coordinator()
    {
        await using var db = Db(); var f = await Seed(db);
        var result = await Controller(db, f.Sender.Id).ConfirmReceipt(f.Task.Id, Receipt(), default);
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(await db.ReceiptConfirmations.ToListAsync());
        Assert.Equal(ReceiptStatus.AwaitingConfirmation, f.Task.ReceiptStatus);
    }

    [Fact]
    public async Task Receiver_confirms_receipt_with_trusted_actor_and_sender_can_read_history()
    {
        await using var db = Db(); var f = await Seed(db);
        var result = await Controller(db, f.Receiver.Id).ConfirmReceipt(f.Task.Id, Receipt(), default);
        Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(f.Receiver.Id, (await db.ReceiptConfirmations.SingleAsync()).ActingUserId);
        Assert.Equal(ReceiptStatus.ReceiptConfirmed, f.Task.ReceiptStatus);
        var history = await Controller(db, f.Sender.Id).Receipts(f.Task.Id, default);
        Assert.Single(Assert.IsType<List<ReceiptItem>>(Assert.IsType<OkObjectResult>(history.Result).Value));
    }

    [Fact]
    public async Task Internal_receipt_requires_explicit_receiver_and_cannot_predate_sending()
    {
        await using var db = Db(); var f = await Seed(db);
        await Assert.ThrowsAsync<TrackingValidationException>(() => Controller(db, f.Receiver.Id).ConfirmReceipt(f.Task.Id,
            Receipt() with { ReceivedAtUtc = DateTime.UtcNow.AddDays(-1) }, default));
        f.Task.ReceivingDivisionId = null; await db.SaveChangesAsync();
        Assert.False(await Access(db, f.Receiver.Id).AuthorizedReceiptObligations().AnyAsync(x => x.Id == f.Task.Id));
        Assert.Empty(await db.ReceiptConfirmations.ToListAsync());
    }

    [Fact]
    public async Task Evidence_uploader_cannot_verify_own_proof_but_another_scoped_coordinator_can()
    {
        await using var db = Db(); var f = await Seed(db);
        var evidence = new EvidenceDocument { TrackingObligationId = f.Task.Id, OriginalFileName = "proof.pdf", StorageKey = "test.pdf", ContentType = "application/pdf", SizeBytes = 10, UploadedByUserId = f.Sender.Id };
        db.Add(evidence); await db.SaveChangesAsync();
        var own = await Controller(db, f.Sender.Id).Evidence(f.Task.Id, default);
        Assert.False(Assert.Single(Assert.IsType<List<EvidenceItem>>(Assert.IsType<OkObjectResult>(own.Result).Value)).CanReview);
        var config = new ConfigurationBuilder().Build();
        await Assert.ThrowsAsync<TrackingConflictException>(() => new EvidenceController(db, Access(db, f.Sender.Id), config, null!).Decide(evidence.Id, new EvidenceDecisionRequest("Verified", null), default));
        Assert.Empty(await db.EvidenceDecisions.ToListAsync());
        var review = await Controller(db, f.Coordinator.Id).Evidence(f.Task.Id, default);
        Assert.True(Assert.Single(Assert.IsType<List<EvidenceItem>>(Assert.IsType<OkObjectResult>(review.Result).Value)).CanReview);
        Assert.IsType<NoContentResult>(await new EvidenceController(db, Access(db, f.Coordinator.Id), config, null!).Decide(evidence.Id, new EvidenceDecisionRequest("Verified", null), default));
        Assert.Equal(f.Coordinator.Id, (await db.EvidenceDecisions.SingleAsync()).DecidedByUserId);
    }

    [Fact]
    public async Task Reviewer_permission_is_report_scoped_and_receipt_access_does_not_grant_review()
    {
        await using var db = Db(); var f = await Seed(db);
        var evidence = new EvidenceDocument { TrackingObligationId = f.Task.Id, OriginalFileName = "proof.pdf", StorageKey = "test.pdf", ContentType = "application/pdf", SizeBytes = 10, UploadedByUserId = f.Sender.Id };
        db.Add(evidence); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new EvidenceController(db, Access(db, f.Receiver.Id), new ConfigurationBuilder().Build(), null!).Decide(evidence.Id, new EvidenceDecisionRequest("Verified", null), default));
        var grant = await db.UserRoles.SingleAsync(x => x.UserId == f.Coordinator.Id);
        grant.ReportDefinitionId = Guid.NewGuid(); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new EvidenceController(db, Access(db, f.Coordinator.Id), new ConfigurationBuilder().Build(), null!).Decide(evidence.Id, new EvidenceDecisionRequest("Verified", null), default));
        Assert.Empty(await db.EvidenceDecisions.ToListAsync());
    }

    [Fact]
    public async Task BB_receipt_requires_separate_scoped_coordinator_and_receiver_assignment_is_audited()
    {
        await using var db = Db(); var f = await Seed(db);
        Assert.IsType<NoContentResult>(await Controller(db, f.Coordinator.Id).AssignReceiver(f.Task.Id, new AssignReceivingDivisionRequest(f.Receiver.DivisionId), default));
        Assert.Equal("ReceivingDivisionAssigned", (await db.AuditEvents.SingleAsync()).Action);
        f.Task.Type = ObligationType.External; f.Task.ReceivingDivisionId = null; await db.SaveChangesAsync();
        Assert.False(await Access(db, f.Receiver.Id).AuthorizedReceiptObligations().AnyAsync(x => x.Id == f.Task.Id));
        Assert.True(await Access(db, f.Coordinator.Id).AuthorizedReceiptObligations().AnyAsync(x => x.Id == f.Task.Id));
        Assert.False(await Access(db, f.Sender.Id).AuthorizedReceiptObligations().AnyAsync(x => x.Id == f.Task.Id));
    }

    private static ReceiptRequest Receipt() => new(DateTime.UtcNow.AddMinutes(-15), false, null);
    private static ReportTrackerDbContext Db() => new(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static TrackingAccess Access(ReportTrackerDbContext db, Guid actor) => new(db, new Actor(actor));
    private static ObligationOperationsController Controller(ReportTrackerDbContext db, Guid actor) => new(db, Access(db, actor), new ConfigurationBuilder().Build(), null!);
    private sealed record Actor(Guid UserId) : ICurrentUser;
    private sealed record Fixture(TrackingObligation Task, AppUser Sender, AppUser Receiver, AppUser Coordinator, AppUser Stranger);
    private static async Task<Fixture> Seed(ReportTrackerDbContext db)
    {
        var sendingDivision = new Division { Code = "SEND", Name = "Sending" };
        var receivingDivision = new Division { Code = "RECV", Name = "Receiving" };
        var otherDivision = new Division { Code = "OTHER", Name = "Other" };
        AppUser User(string code, Guid division) => new() { EmployeeId = code, DisplayName = code, Email = code + "@example.test", DivisionId = division };
        var sender = User("sender", sendingDivision.Id); var receiver = User("receiver", receivingDivision.Id);
        var coordinator = User("coordinator", sendingDivision.Id); var stranger = User("stranger", otherDivision.Id);
        var report = new ReportDefinition { Code = "RIT", Name = "RIT", OwnerDivisionId = sendingDivision.Id, BbRecipient = "BB" };
        var cycle = new ReportCycle { ReportDefinition = report, ReportDefinitionId = report.Id, PeriodKey = "2026-11", PeriodLabel = "November" };
        var task = new TrackingObligation { Name = "Internal figures", Type = ObligationType.Internal, ReportCycle = cycle, ReportCycleId = cycle.Id,
            AccountableDivisionId = sendingDivision.Id, ReceivingDivisionId = receivingDivision.Id, PrimaryOfficerId = sender.Id, Recipient = "Receiving",
            ReceiptStatus = ReceiptStatus.AwaitingConfirmation, OriginalDueAtUtc = DateTime.UtcNow, EffectiveDueAtUtc = DateTime.UtcNow };
        task.SendingEvents.Add(new SendingEvent { ActingUserId = sender.Id, ActualSentAtUtc = DateTime.UtcNow.AddHours(-1), Recipient = "Receiving", Channel = "Email" });
        var role = new AppRole { Name = "Coordinator" }; var permission = new Permission { Code = "reports.manage", Name = "Manage reports" };
        db.AddRange(sendingDivision, receivingDivision, otherDivision, sender, receiver, coordinator, stranger, task, role, permission,
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
            new UserRole { UserId = sender.Id, RoleId = role.Id, DivisionId = sendingDivision.Id, ReportDefinitionId = report.Id },
            new UserRole { UserId = coordinator.Id, RoleId = role.Id, DivisionId = sendingDivision.Id, ReportDefinitionId = report.Id });
        await db.SaveChangesAsync(); return new(task, sender, receiver, coordinator, stranger);
    }
}
