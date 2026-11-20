using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Persistence.OracleMigrations;

namespace ReportTracker.Tests;

public sealed class AssigneeTerminologyTests
{
    [Fact]
    public void Assignment_contract_round_trips_generic_names_and_optional_alternate()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var pair = new AssigneePair(Guid.NewGuid(), null);
        var json = JsonSerializer.Serialize(pair, options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(pair.PrimaryAssigneeId, document.RootElement.GetProperty("primaryAssigneeId").GetGuid());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("alternateAssigneeId").ValueKind);
        Assert.False(document.RootElement.TryGetProperty("primaryOfficerId", out _));
        Assert.False(document.RootElement.TryGetProperty("deputyOfficerId", out _));
        Assert.Equal(pair, JsonSerializer.Deserialize<AssigneePair>(json, options));
    }

    [Fact]
    public void Oracle_upgrade_and_rollback_rename_all_assignment_columns_without_dropping_data()
    {
        var migration = new RenameAssignmentAssignees();
        var changes = migration.UpOperations.OfType<RenameColumnOperation>().ToArray();
        Assert.Equal(6, changes.Length);
        foreach (var table in new[] { "rt_component_definitions", "rt_tracking_obligations", "rt_responsibility_assignments" })
        {
            Assert.Contains(changes, x => x.Table == table && x.Name == "primary_officer_id" && x.NewName == "primary_assignee_id");
            Assert.Contains(changes, x => x.Table == table && x.Name == "deputy_officer_id" && x.NewName == "alternate_assignee_id");
        }
        Assert.All(migration.UpOperations, x => Assert.True(x is RenameColumnOperation or SqlOperation));
        Assert.Equal(6, migration.DownOperations.OfType<RenameColumnOperation>().Count());
        Assert.Contains(migration.UpOperations.OfType<SqlOperation>(), x => x.Sql.Contains("rt_reminder_policies") && x.Sql.Contains("Alternate"));
    }

    [Theory]
    [InlineData("Primary,Alternate")]
    [InlineData("Primary,Deputy")]
    [InlineData("Primary,Alternate,Deputy")]
    public async Task Reminders_reach_primary_and_alternate_including_legacy_policies(string recipients)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Oracle"] = "User Id=unused;Password=unused;Data Source=unused",
            ["Automation:Enabled"] = "false"
        }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.RemoveAll<DbContextOptions<ReportTrackerDbContext>>();
        services.AddSingleton(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportTrackerDbContext>();
        var division = new Division { Code = "D", Name = "Division" };
        var primary = new AppUser { EmployeeId = "P", DisplayName = "Primary", Email = "primary@example.test", DivisionId = division.Id };
        var alternate = new AppUser { EmployeeId = "A", DisplayName = "Alternate", Email = "alternate@example.test", DivisionId = division.Id };
        var report = new ReportDefinition { Code = "R", Name = "Report", OwnerDivisionId = division.Id, BbRecipient = "Recipient" };
        var cycle = new ReportCycle { ReportDefinition = report, PeriodKey = "2026-11", PeriodLabel = "November" };
        var now = new DateTime(2026, 11, 21, 6, 0, 0, DateTimeKind.Utc);
        var task = new TrackingObligation { ReportCycle = cycle, Name = "Submission", Recipient = "Recipient",
            AccountableDivisionId = division.Id, PrimaryAssigneeId = primary.Id, AlternateAssigneeId = alternate.Id, EffectiveDueAtUtc = now };
        db.AddRange(division, primary, alternate, report, cycle, task, new ReminderPolicy { Name = "Reminder", ObligationCategory = "All",
            ChannelsCsv = "Email", RecipientsCsv = recipients, LeadDaysCsv = "0", WorkingDaysCsv = "Sun,Mon,Tue,Wed,Thu,Fri,Sat" });
        await db.SaveChangesAsync();
        var automation = scope.ServiceProvider.GetRequiredService<INotificationAutomationService>();
        Assert.Equal(2, await automation.PlanAsync(now, default));
        Assert.Equal(new[] { alternate.Email, primary.Email }.Order(), (await db.NotificationJobs.Select(x => x.Recipient).ToArrayAsync()).Order());
        Assert.Equal(0, await automation.PlanAsync(now, default));
    }
}
