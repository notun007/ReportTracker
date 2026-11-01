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
        Assert.True(await service.RecordSendingAsync(own.Id, Command(), default));
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
        Assert.True(await service.RecordSendingAsync(own.Id, Command(true), default));
        Assert.Equal(SendingStatus.PartiallySent, own.SendingStatus);
        Assert.Equal(1, (await service.GetDashboardAsync(default)).MyPending);
    }

    private static RecordSendingCommand Command(bool partial = false) => new(DateTime.UtcNow.AddMinutes(-1), "BB", "Portal", "REF", partial,
        partial ? ["Card"] : null, null, Convert.ToBase64String(new byte[8]));

    private static ServiceProvider Provider(Guid actor)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ReportTracker"] = "Server=unused" }).Build());
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
