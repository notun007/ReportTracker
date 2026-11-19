using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure;
using ReportTracker.Infrastructure.Persistence;

namespace ReportTracker.Tests;

public sealed class TrackingWorkflowTests
{
    [Fact]
    public void Due_today_uses_Dhaka_calendar_boundary()
    {
        var now = new DateTime(2026, 11, 1, 19, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Due Today", TrackingRules.Timeliness(due, null, now));
    }

    [Theory]
    [InlineData(-1, "Sent On Time")]
    [InlineData(1, "Sent Late")]
    public void Timeliness_uses_actual_sending_time_not_entry_time(int offset, string expected)
    {
        var due = DateTime.UtcNow.AddDays(-2);
        Assert.Equal(expected, TrackingRules.Timeliness(due, due.AddMinutes(offset), DateTime.UtcNow));
    }

    [Fact]
    public void Partial_sending_requires_component_names()
    {
        Assert.Throws<TrackingValidationException>(() => TrackingRules.ValidateSending(Command(true) with { ScopeNames = [] }, DateTime.UtcNow));
    }

    [Fact]
    public void Sending_rejects_non_UTC_and_future_dates()
    {
        Assert.Throws<TrackingValidationException>(() => TrackingRules.ValidateSending(Command() with { ActualSentAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) }, DateTime.UtcNow));
        Assert.Throws<TrackingValidationException>(() => TrackingRules.ValidateSending(Command() with { ActualSentAtUtc = DateTime.UtcNow.AddHours(1) }, DateTime.UtcNow));
    }

    [Fact]
    public async Task Missing_identity_cannot_read_an_unscoped_dashboard()
    {
        await using var provider = Provider(Guid.Empty);
        var service = provider.GetRequiredService<ITrackingService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDashboardAsync(default));
    }

    [Fact]
    public async Task Officers_only_see_and_update_their_assigned_obligations()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var own = Seed(db, actor);
        var other = Seed(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();
        var dashboard = await service.GetDashboardAsync(default);
        Assert.Single(dashboard.Upcoming);
        Assert.True(dashboard.Upcoming[0].IsAssignedToMe);
        Assert.Equal(own.Id, dashboard.Upcoming[0].Id);
        Assert.Null(await service.GetObligationAsync(other.Id, default));
        Assert.False(await service.RecordSendingAsync(other.Id, Command(), default));
    }

    [Fact]
    public async Task Dashboard_and_detail_return_accountable_division_separately_from_officer()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var own = Seed(db, actor);
        var division = db.Divisions.Local.Single(x => x.Id == own.AccountableDivisionId);
        division.Name = "Finance Division";
        var other = Seed(db, Guid.NewGuid());
        own.PrimaryOfficerId = other.PrimaryOfficerId;
        own.DeputyOfficerId = actor;
        await db.SaveChangesAsync();

        var service = provider.GetRequiredService<ITrackingService>();
        var row = Assert.Single((await service.GetDashboardAsync(default)).Upcoming);
        Assert.Equal(division.Id, row.DivisionId);
        Assert.Equal("Finance Division", row.Division);
        Assert.DoesNotContain("Finance Division", row.Owner);
        var detail = await service.GetObligationAsync(own.Id, default);
        Assert.NotNull(detail);
        Assert.Equal(row.DivisionId, detail.DivisionId);
        Assert.Equal(row.Division, detail.Division);
    }

    [Fact]
    public async Task Component_recipient_displays_the_receiving_division_name_for_legacy_guid_values()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var component = Seed(db, actor);
        var receiving = new Division { Code = "RCV", Name = "Information Security Division" };
        component.Type = ObligationType.Internal;
        component.ReceivingDivisionId = receiving.Id;
        component.Recipient = receiving.Id.ToString();
        db.Add(receiving);
        await db.SaveChangesAsync();

        var service = provider.GetRequiredService<ITrackingService>();
        var row = Assert.Single((await service.GetDashboardAsync(default)).Upcoming);
        var detail = await service.GetObligationAsync(component.Id, default);

        Assert.Equal(receiving.Name, row.Recipient);
        Assert.Equal(receiving.Name, detail!.Recipient);
    }

    [Fact]
    public async Task Read_permission_is_scoped_and_does_not_grant_sending_authority()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var visible = Seed(db, actor);
        var hidden = Seed(db, Guid.NewGuid());
        visible.PrimaryOfficerId = hidden.PrimaryOfficerId;
        var role = new AppRole { Name = "Division viewer" };
        var permission = new Permission { Code = "tracking.read", Name = "Read tracking" };
        db.AddRange(role, permission, new UserRole { UserId = actor, RoleId = role.Id, DivisionId = visible.AccountableDivisionId },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();
        var dashboard = await service.GetDashboardAsync(default);
        Assert.Equal(0, dashboard.MyPending);
        var row = Assert.Single(dashboard.Upcoming);
        Assert.False(row.CanRecordSending);
        Assert.False(row.IsAssignedToMe);
        Assert.Equal(visible.Id, row.Id);
        Assert.Null(await service.GetObligationAsync(hidden.Id, default));
        Assert.False(await service.RecordSendingAsync(visible.Id, Command(), default));
    }

    [Fact]
    public async Task Sending_persists_the_authenticated_actor_and_preserves_other_obligations()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var own = Seed(db, actor);
        var other = Seed(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();
        Assert.True(await service.RecordSendingAsync(own.Id, Command(rowVersion: own.RowVersion), default));
        Assert.Equal(actor, (await db.SendingEvents.SingleAsync()).ActingUserId);
        Assert.Equal(actor, (await db.AuditEvents.SingleAsync()).ActorUserId);
        Assert.Equal(SendingStatus.Sent, own.SendingStatus);
        Assert.Equal(SendingStatus.NotSent, other.SendingStatus);
        Assert.Equal(ReceiptStatus.AwaitingConfirmation, own.ReceiptStatus);
        Assert.Equal(EvidenceStatus.Missing, own.EvidenceStatus);
        await Assert.ThrowsAsync<TrackingConflictException>(() => service.RecordSendingAsync(own.Id, Command(), default));
    }

    [Fact]
    public async Task Partial_sending_keeps_the_obligation_pending()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var own = Seed(db, actor);
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();
        Assert.True(await service.RecordSendingAsync(own.Id, Command(true, own.RowVersion), default));
        Assert.Equal(SendingStatus.PartiallySent, own.SendingStatus);
        Assert.Equal(1, (await service.GetDashboardAsync(default)).MyPending);
    }

    [Fact]
    public async Task Dashboard_derives_main_report_readiness_from_completed_components()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var main = Seed(db, actor);
        main.Type = ObligationType.External;
        var component = new TrackingObligation
        {
            Name = "Completed component", Type = ObligationType.Internal, ReportCycle = main.ReportCycle,
            ReportCycleId = main.ReportCycleId, ParentObligationId = main.Id, AccountableDivisionId = main.AccountableDivisionId,
            PrimaryOfficerId = actor, Recipient = "Main sender", OriginalDueAtUtc = main.OriginalDueAtUtc.AddDays(-1),
            EffectiveDueAtUtc = main.EffectiveDueAtUtc.AddDays(-1), SendingStatus = SendingStatus.Sent,
            EvidenceStatus = EvidenceStatus.Verified, ReceiptStatus = ReceiptStatus.ReceiptConfirmed, RowVersion = new byte[8]
        };
        db.Add(component);
        await db.SaveChangesAsync();

        var dashboard = await provider.GetRequiredService<ITrackingService>().GetDashboardAsync(default);

        Assert.Equal(1, dashboard.ReadyForSubmission);
        var mainRow = dashboard.Upcoming.Single(x => x.Id == main.Id);
        Assert.True(mainRow.IsReadyForSubmission);
        Assert.Equal(1, mainRow.RequiredComponentCount);
        Assert.Equal(1, mainRow.CompletedComponentCount);
    }

    [Theory]
    [InlineData(SendingStatus.NotSent, ReceiptStatus.AwaitingConfirmation, EvidenceStatus.Missing)]
    [InlineData(SendingStatus.PartiallySent, ReceiptStatus.ReceiptConfirmed, EvidenceStatus.Verified)]
    [InlineData(SendingStatus.Sent, ReceiptStatus.AwaitingConfirmation, EvidenceStatus.Verified)]
    [InlineData(SendingStatus.Sent, ReceiptStatus.ReceiptDisputed, EvidenceStatus.Verified)]
    [InlineData(SendingStatus.Sent, ReceiptStatus.ReceiptConfirmed, EvidenceStatus.Provided)]
    [InlineData(SendingStatus.Sent, ReceiptStatus.ReceiptConfirmed, EvidenceStatus.Rejected)]
    public async Task Pending_components_from_another_division_keep_period_visible_and_block_final_submission(
        SendingStatus sending, ReceiptStatus receipt, EvidenceStatus evidence)
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var main = Seed(db, actor);
        var component = Seed(db, Guid.NewGuid());
        component.Type = ObligationType.Internal;
        component.Name = "Finance input";
        component.ReportCycle = main.ReportCycle;
        component.ReportCycleId = main.ReportCycleId;
        component.ParentObligationId = main.Id;
        component.SendingStatus = sending;
        component.ReceiptStatus = receipt;
        component.EvidenceStatus = evidence;
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();

        var row = Assert.Single((await service.GetDashboardAsync(default)).Upcoming);
        Assert.Equal(main.Id, row.Id);
        Assert.False(row.IsReadyForSubmission);
        Assert.False(row.CanRecordSending);
        Assert.Equal(1, row.RequiredComponentCount);
        Assert.Equal(component.Id, Assert.Single(row.PendingComponents).Id);
        Assert.False((await service.GetObligationAsync(main.Id, default))!.IsReadyForSubmission);
        await Assert.ThrowsAsync<TrackingConflictException>(() => service.RecordSendingAsync(main.Id, Command(rowVersion: main.RowVersion), default));
        Assert.Empty(db.SendingEvents);
        Assert.Empty(db.AuditEvents);

        component.SendingStatus = SendingStatus.Sent;
        component.ReceiptStatus = ReceiptStatus.ReceiptConfirmed;
        component.EvidenceStatus = EvidenceStatus.Verified;
        await db.SaveChangesAsync();
        row = Assert.Single((await service.GetDashboardAsync(default)).Upcoming);
        Assert.True(row.IsReadyForSubmission);
        Assert.True(row.CanRecordSending);
        Assert.Empty(row.PendingComponents);
        Assert.True(await service.RecordSendingAsync(main.Id, Command(rowVersion: main.RowVersion), default));
        Assert.Equal(0, (await service.GetDashboardAsync(default)).ReadyForSubmission);
    }

    [Fact]
    public async Task Optional_and_cancelled_components_do_not_block_final_submission()
    {
        var actor = Guid.NewGuid();
        await using var provider = Provider(actor);
        var db = provider.GetRequiredService<ReportTrackerDbContext>();
        var main = Seed(db, actor);
        foreach (var required in new[] { false, true })
            db.Add(new TrackingObligation
            {
                Name = "Excluded component", Recipient = "Main sender", Type = ObligationType.Internal, ReportCycle = main.ReportCycle,
                ReportCycleId = main.ReportCycleId, ParentObligationId = main.Id,
                AccountableDivisionId = main.AccountableDivisionId, IsRequired = required,
                ExceptionStatus = required ? ExceptionStatus.Cancelled : ExceptionStatus.None
            });
        await db.SaveChangesAsync();
        var service = provider.GetRequiredService<ITrackingService>();
        var row = (await service.GetDashboardAsync(default)).Upcoming.Single(x => x.Id == main.Id);
        Assert.Equal(0, row.RequiredComponentCount);
        Assert.True(row.IsReadyForSubmission);
        Assert.True(row.CanRecordSending);
        Assert.True(await service.RecordSendingAsync(main.Id, Command(rowVersion: main.RowVersion), default));
    }

    [Fact]
    public void Reporting_frequency_supports_biweekly_cycles()
    {
        Assert.True(Enum.IsDefined(Frequency.Biweekly));
    }

    private static RecordSendingCommand Command(bool partial = false, byte[]? rowVersion = null) =>
        new(DateTime.UtcNow.AddMinutes(-1), "BB", "Portal", "REF", partial,
            partial ? ["Card"] : null, null, Convert.ToBase64String(rowVersion ?? new byte[8]));

    private static ServiceProvider Provider(Guid actor)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Oracle"] = "User Id=unused;Password=unused;Data Source=unused"
        }).Build());
        services.RemoveAll<DbContextOptions<ReportTrackerDbContext>>();
        services.AddSingleton(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        services.AddSingleton<ICurrentUser>(new CurrentUser(actor));
        return services.BuildServiceProvider();
    }

    private static TrackingObligation Seed(ReportTrackerDbContext db, Guid userId)
    {
        var division = new Division { Code = Guid.NewGuid().ToString(), Name = "Test" };
        var user = new AppUser { Id = userId, EmployeeId = userId.ToString(), DisplayName = "Officer", Email = userId + "@example.test", DivisionId = division.Id };
        var report = new ReportDefinition { Code = Guid.NewGuid().ToString(), Name = "Test report", OwnerDivisionId = division.Id, BbRecipient = "BB" };
        var cycle = new ReportCycle { ReportDefinition = report, PeriodKey = "2026-10", PeriodLabel = "October", ReportDefinitionId = report.Id };
        var obligation = new TrackingObligation { Name = "BB submission", Type = ObligationType.External, ReportCycle = cycle, ReportCycleId = cycle.Id,
            AccountableDivisionId = division.Id, PrimaryOfficerId = userId, Recipient = "BB", EffectiveDueAtUtc = DateTime.UtcNow.AddDays(1),
            OriginalDueAtUtc = DateTime.UtcNow.AddDays(1), RowVersion = new byte[8], ReceiptStatus = ReceiptStatus.AwaitingConfirmation, EvidenceStatus = EvidenceStatus.Missing };
        db.AddRange(division, user, obligation);
        return obligation;
    }

    private sealed record CurrentUser(Guid UserId) : ICurrentUser;
}
