using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oracle.EntityFrameworkCore;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Domain.Enums;
using ReportTracker.Infrastructure;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class ComponentContributorAccessTests
{
    [Fact]
    public async Task Officers_see_only_their_components_including_shared_components_in_dashboard_and_direct_links()
    {
        await using var f = await Fixture.Create();
        foreach (var (officer, own, hidden) in new[] { (f.Rahim, f.A, f.B), (f.Karim, f.B, f.A) })
        {
            f.Actor.UserId = officer.Id;
            var dashboard = await f.Service.GetDashboardAsync(default);
            Assert.Equal(new[] { own.Id, f.C.Id }.Order(), dashboard.Upcoming.Select(x => x.Id).Order());
            Assert.Equal(2, dashboard.MyPending);
            Assert.All(dashboard.Upcoming, row => Assert.True(row.IsAssignedToMe));
            Assert.True(dashboard.Upcoming.Single(x => x.Id == own.Id).CanRecordSending);
            Assert.False(dashboard.Upcoming.Single(x => x.Id == f.C.Id).CanRecordSending);
            Assert.NotNull(await f.Service.GetObligationAsync(f.C.Id, default));
            Assert.Null(await f.Service.GetObligationAsync(hidden.Id, default));
            Assert.Null(await f.Service.GetHistoryAsync(hidden.Id, default));
        }
        f.Actor.UserId = f.Unassigned.Id;
        Assert.Empty((await f.Service.GetDashboardAsync(default)).Upcoming);
        Assert.Null(await f.Service.GetObligationAsync(f.C.Id, default));
    }

    [Theory]
    [InlineData("LeadPreparer")]
    [InlineData("Preparer")]
    [InlineData("BackupPreparer")]
    [InlineData("Reviewer")]
    public async Task Contributor_roles_grant_read_access_without_implicitly_granting_actions(string role)
    {
        await using var f = await Fixture.Create();
        f.C.ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Unassigned.Id, role) });
        f.Db.UserRoles.Add(new UserRole {UserId=f.Unassigned.Id,RoleId=(await f.Db.Roles.SingleAsync(x=>x.Code=="REPORT_PREPARER")).Id,ReportDefinitionId=f.C.ReportCycle.ReportDefinitionId});
        await f.Db.SaveChangesAsync();
        f.Actor.UserId = f.Unassigned.Id;
        var actions = Assert.IsType<ObligationActions>(Assert.IsType<OkObjectResult>((await f.Operations.Actions(f.C.Id, default)).Result).Value);
        Assert.False(actions.CanUploadEvidence);
        Assert.False(actions.CanAmendStatus);
        Assert.False(actions.CanConfirmReceipt);
        Assert.False(actions.CanReviewEvidence);
        Assert.False(actions.CanAssignReceiver);
        Assert.False(await f.Service.RecordSendingAsync(f.C.Id, Sending(f.C), default));
        Assert.IsType<NotFoundResult>((await f.Operations.UploadEvidence(f.C.Id, null!, default)).Result);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Evidence.Decide(f.Proof.Id, new("Verified", null), default));
        Assert.Empty(f.Db.EvidenceDecisions);
    }

    [Fact]
    public async Task History_receipts_and_evidence_downloads_follow_component_visibility()
    {
        await using var f = await Fixture.Create();
        var root = Path.Combine(Path.GetTempPath(), "ReportTrackerContributor-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, f.Proof.StorageKey);
        await File.WriteAllTextAsync(file, "%PDF-test");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EvidenceStorage:Root"] = root }).Build();
        var controller = new EvidenceController(f.Db, f.Access, config, null!);
        try
        {
            foreach (var officer in new[] { f.Rahim, f.Karim })
            {
                f.Actor.UserId = officer.Id;
                Assert.Single((await f.Service.GetHistoryAsync(f.C.Id, default))!);
                Assert.IsType<OkObjectResult>((await f.Operations.Receipts(f.C.Id, default)).Result);
                var evidence = Assert.IsType<List<EvidenceItem>>(Assert.IsType<OkObjectResult>((await f.Operations.Evidence(f.C.Id, default)).Result).Value);
                Assert.False(Assert.Single(evidence).CanReview);
                Assert.IsType<PhysicalFileResult>(await controller.Download(f.Proof.Id, default));
            }
            f.Actor.UserId = f.Unassigned.Id;
            Assert.Null(await f.Service.GetHistoryAsync(f.C.Id, default));
            Assert.IsType<NotFoundResult>((await f.Operations.Actions(f.C.Id, default)).Result);
            Assert.IsType<NotFoundResult>((await f.Operations.Receipts(f.C.Id, default)).Result);
            Assert.IsType<NotFoundResult>((await f.Operations.Evidence(f.C.Id, default)).Result);
            Assert.IsType<NotFoundResult>(await controller.Download(f.Proof.Id, default));
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task Period_snapshot_controls_access_independently_of_current_component_defaults()
    {
        await using var f = await Fixture.Create();
        var definition = new ComponentDefinition
        {
            Name = "C", ProviderDivisionId = f.Rahim.DivisionId, ReceivingDivisionId = Guid.NewGuid(),
            ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Unassigned.Id, "Preparer") })
        };
        f.Db.Add(definition);
        await f.Db.SaveChangesAsync();
        f.Actor.UserId = f.Unassigned.Id;
        Assert.False(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
        f.Actor.UserId = f.Rahim.Id;
        Assert.True(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
        // Even a legacy period without JSON must not inherit today's default contributors.
        f.C.ContributorsJson = null;
        await f.Db.SaveChangesAsync();
        Assert.False(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
        Assert.True(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.A.Id));
        f.Actor.UserId = f.Lead.Id;
        Assert.True(await f.Access.AuthorizedObligations(true).AnyAsync(x => x.Id == f.C.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Missing_or_invalid_contributor_metadata_does_not_grant_access(string? json)
    {
        await using var f = await Fixture.Create();
        f.C.ContributorsJson = json;
        await f.Db.SaveChangesAsync();
        f.Actor.UserId = f.Rahim.Id;
        Assert.False(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
        Assert.True(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.A.Id));
    }

    [Fact]
    public async Task Unknown_roles_and_external_obligation_metadata_do_not_grant_contributor_access()
    {
        await using var f = await Fixture.Create();
        f.Actor.UserId = f.Unassigned.Id;
        f.C.ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Unassigned.Id, "Unknown") });
        await f.Db.SaveChangesAsync();
        Assert.False(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
        f.C.ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Unassigned.Id, "Preparer") });
        f.C.Type = ObligationType.External;
        await f.Db.SaveChangesAsync();
        Assert.False(await f.Access.AuthorizedObligations().AnyAsync(x => x.Id == f.C.Id));
    }

    [Fact]
    public async Task Inactive_officers_cannot_use_contributor_or_direct_assignments()
    {
        await using var f = await Fixture.Create();
        f.Rahim.IsActive = false;
        await f.Db.SaveChangesAsync();
        f.Actor.UserId = f.Rahim.Id;
        Assert.Empty(await f.Access.AuthorizedObligations().ToListAsync());
        Assert.Empty(await f.Access.AuthorizedObligations(true).ToListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetDashboardAsync(default));
        Assert.IsType<NotFoundResult>(await f.Evidence.Download(f.Proof.Id, default));
    }

    [Fact]
    public void Oracle_composes_parameterized_snapshot_access_with_detail_and_history_queries()
    {
        using var db = new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseOracle("User Id=model_test;Password=model_test;Data Source=localhost/model_test",
                options => options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        var actor = new Actor { UserId = Guid.NewGuid() };
        var access = new TrackingAccess(db, actor);
        var readable = access.AuthorizedObligations();
        var sql = readable.Include(x => x.ReportCycle).ThenInclude(x => x.ReportDefinition)
            .Include(x => x.SendingEvents).Where(x => x.Id == Guid.NewGuid()).ToQueryString();
        Assert.Contains("JSON_EXISTS(TO_CLOB", sql);
        Assert.Contains("PASSING CAST(:", sql);
        Assert.Contains("FALSE ON ERROR", sql);
        var ids = readable.Select(x => x.Id);
        Assert.Contains("JSON_EXISTS", db.SendingEvents.Where(x => ids.Contains(x.TrackingObligationId)).ToQueryString());
        Assert.DoesNotContain("JSON_EXISTS", access.AuthorizedObligations(true).ToQueryString());
    }

    private static RecordSendingCommand Sending(TrackingObligation task) => new(DateTime.UtcNow.AddMinutes(-1), "Receiving", "Email", null,
        false, null, null, Convert.ToBase64String(task.RowVersion));

    private sealed class Actor : ICurrentUser { public Guid UserId { get; set; } }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        public Actor Actor { get; } = new();
        public ReportTrackerDbContext Db => provider.GetRequiredService<ReportTrackerDbContext>();
        public TrackingAccess Access => new(Db, Actor);
        public ITrackingService Service => provider.GetRequiredService<ITrackingService>();
        public ObligationOperationsController Operations => new(Db, Access, new ConfigurationBuilder().Build(), null!);
        public EvidenceController Evidence => new(Db, Access, new ConfigurationBuilder().Build(), null!);
        public AppUser Rahim { get; private set; } = null!;
        public AppUser Karim { get; private set; } = null!;
        public AppUser Lead { get; private set; } = null!;
        public AppUser Unassigned { get; private set; } = null!;
        public TrackingObligation A { get; private set; } = null!;
        public TrackingObligation B { get; private set; } = null!;
        public TrackingObligation C { get; private set; } = null!;
        public EvidenceDocument Proof { get; private set; } = null!;

        private Fixture()
        {
            var services = new ServiceCollection();
            services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:Oracle"] = "User Id=unused;Password=unused;Data Source=unused", ["Automation:Enabled"] = "false" }).Build());
            services.RemoveAll<DbContextOptions<ReportTrackerDbContext>>();
            services.AddSingleton(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            services.AddSingleton<ICurrentUser>(Actor);
            provider = services.BuildServiceProvider();
        }

        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var division = new Division { Code = "FIN", Name = "Finance" };
            AppUser Officer(string name) => new() { EmployeeId = name, DisplayName = name, Email = name + "@example.test", DivisionId = division.Id };
            f.Rahim = Officer("Rahim"); f.Karim = Officer("Karim"); f.Lead = Officer("Lead"); f.Unassigned = Officer("Unassigned");
            var report = new ReportDefinition { Code = "R", Name = "Report", OwnerDivisionId = division.Id, BbRecipient = "BB" };
            var cycle = new ReportCycle { ReportDefinition = report, ReportDefinitionId = report.Id, PeriodKey = "2026-11", PeriodLabel = "November" };
            TrackingObligation Task(string name, AppUser lead) => new()
            {
                Name = name, Type = ObligationType.Internal, ReportCycle = cycle, ReportCycleId = cycle.Id,
                AccountableDivisionId = division.Id, PrimaryAssigneeId = lead.Id, Recipient = "Receiving",
                OriginalDueAtUtc = DateTime.UtcNow.AddDays(1), EffectiveDueAtUtc = DateTime.UtcNow.AddDays(1)
            };
            f.A = Task("A", f.Rahim); f.B = Task("B", f.Karim); f.C = Task("C", f.Lead);
            f.C.ContributorsJson = JsonSerializer.Serialize(new[] { new ComponentContributorItem(f.Lead.Id, "LeadPreparer"),
                new ComponentContributorItem(f.Rahim.Id, "Preparer"), new ComponentContributorItem(f.Karim.Id, "Reviewer") });
            f.C.SendingEvents.Add(new SendingEvent { ActingUserId = f.Lead.Id, ActualSentAtUtc = DateTime.UtcNow.AddHours(-1), Recipient = "Receiving", Channel = "Email", IsPartial = true });
            f.Proof = new EvidenceDocument { TrackingObligationId = f.C.Id, UploadedByUserId = f.Lead.Id, OriginalFileName = "proof.pdf", StorageKey = "proof.pdf", ContentType = "application/pdf", SizeBytes = 9 };
            f.Db.AddRange(division, f.Rahim, f.Karim, f.Lead, f.Unassigned, f.A, f.B, f.C, f.Proof);
            await f.Db.SaveChangesAsync(); await AccessControlSeed.PrepareAsync(f.Db, true); await f.Db.SaveChangesAsync(); return f;
        }

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }
}
