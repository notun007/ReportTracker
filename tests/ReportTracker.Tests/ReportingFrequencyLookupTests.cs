using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Api.Controllers;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Domain.Entities;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Tests;

public sealed class ReportingFrequencyLookupTests
{
    [Fact]
    public async Task Administrator_can_create_and_update_supported_reporting_frequency()
    {
        await using var db = new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new AppUser { EmployeeId = "ADMIN", DisplayName = "Administrator", Email = "admin@example.test" };
        var role = new AppRole { Name = "System Administrator" };
        var permission = new Permission { Code = "administration.manage", Name = "Manage administration" };
        db.AddRange(user, role, permission,
            new UserRole { UserId = user.Id, RoleId = role.Id },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();
        var controller = new ReportingFrequenciesController(db, new TrackingAccess(db, new Actor(user.Id)));

        var created = await controller.Create(new CreateReportingFrequencyRequest("MONTHLY", "Calendar month", 3), default);
        var item = Assert.IsType<ReportingFrequencyItem>(Assert.IsType<CreatedResult>(created.Result).Value);
        Assert.Equal("monthly", item.Code);

        var updated = await controller.Update(item.Id, new UpdateReportingFrequencyRequest("Monthly", 4), default);
        var saved = Assert.IsType<ReportingFrequencyItem>(Assert.IsType<OkObjectResult>(updated.Result).Value);
        Assert.Equal("Monthly", saved.Name);
        Assert.Equal(4, saved.DisplayOrder);
    }

    [Theory]
    [InlineData("administration.manage")]
    [InlineData("reports.manage")]
    public async Task Reporting_frequencies_endpoint_returns_lookup_names_and_order_with_no_saved_presets(string permissionCode)
    {
        await using var db = new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var division = new Division { Code = "1001", Name = "Division" };
        var user = new AppUser { EmployeeId = "ADMIN", DisplayName = "Administrator", Email = "admin@example.test", DivisionId = division.Id };
        var role = new AppRole { Name = "System Administrator" };
        var permission = new Permission { Code = permissionCode, Name = permissionCode };
        db.AddRange(division, user, role, permission,
            new UserRole { UserId = user.Id, RoleId = role.Id, DivisionId = permissionCode == "reports.manage" ? division.Id : null },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
            new ReportingFrequency { Code = "monthly", Name = "Calendar month", DisplayOrder = 5 },
            new ReportingFrequency { Code = "yearly", Name = "Calendar year", DisplayOrder = 1 });
        await db.SaveChangesAsync();
        var controller = new ReportingFrequenciesController(db, new TrackingAccess(db, new Actor(user.Id)));

        var result = await controller.List(default);

        var frequencies = Assert.IsAssignableFrom<IReadOnlyList<ReportingFrequencyItem>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new[] { "Calendar year", "Calendar month" }, frequencies.Select(frequency => frequency.Name));
    }

    [Theory]
    [InlineData("twice-monthly", 2, 2, 2, 14, 2, 29)]
    [InlineData("quarterly", 4, 5, 4, 14, 6, 15)]
    [InlineData("half-yearly", 2, 8, 7, 14, 12, 15)]
    [InlineData("monthly", 12, 2, 2, 14, 2, 29)]
    [InlineData("yearly", 1, 11, 2, 14, 10, 20)]
    public async Task Preset_saves_and_previews_custom_periods(string code, int count, int previewMonth,
        int startMonth, int firstEndDay, int endMonth, int endDay)
    {
        await using var db = new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new AppUser { EmployeeId = "ADMIN", DisplayName = "Administrator", Email = "admin@example.test" };
        var role = new AppRole { Name = "System Administrator" };
        var permission = new Permission { Code = "administration.manage", Name = "Manage administration" };
        db.AddRange(user, role, permission,
            new UserRole { UserId = user.Id, RoleId = role.Id },
            new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
            new ReportingFrequency { Code = code, Name = code });
        await db.SaveChangesAsync();
        var controller = new SchedulePresetsController(db, new TrackingAccess(db, new Actor(user.Id)));
        await controller.Create(new CreateSchedulePresetRequest(code), default);
        var periods = Enumerable.Repeat(new WeeklyPresetPeriod(1, null), count).ToArray();
        periods[0] = code == "yearly" ? new(29, 20, 2, 10) : new(2, 14);
        if (count > 1)
            periods[1] = code switch
            {
                "twice-monthly" => new(15, null),
                "monthly" => new(29, null),
                _ => new(30, 15)
            };

        var created = await controller.Create(new CreateSchedulePresetRequest(code, periods), default);
        var preset = Assert.IsType<SchedulePresetItem>(Assert.IsType<ObjectResult>(created.Result).Value);
        Assert.Equal(2, preset.Version);
        Assert.Equal(periods, preset.WeeklyPeriods);

        var preview = await controller.Preview(code, new DateOnly(2028, previewMonth, 1), default);
        var rows = Assert.IsType<SchedulePresetPeriod[]>(Assert.IsType<OkObjectResult>(preview.Result).Value);
        if (code == "twice-monthly")
        {
            Assert.Equal(new DateOnly(2028, startMonth, 2), rows[0].PeriodStart);
            Assert.Equal(new DateOnly(2028, endMonth, firstEndDay), rows[0].PeriodEnd);
        }
        else
        {
            Assert.Equal(new DateOnly(2028, startMonth, code is "monthly" or "yearly" ? 29 : 30), Assert.Single(rows).PeriodStart);
        }
        Assert.Equal(new DateOnly(2028, endMonth, endDay), rows[^1].PeriodEnd);
        if (code == "monthly")
        {
            var nonLeapPreview = await controller.Preview(code, new DateOnly(2027, 2, 1), default);
            var february = Assert.Single(Assert.IsType<SchedulePresetPeriod[]>(Assert.IsType<OkObjectResult>(nonLeapPreview.Result).Value));
            Assert.Equal(new SchedulePresetPeriod(new(2027, 2, 28), new(2027, 2, 28), new(2027, 3, 10)), february);
        }
        if (code == "yearly")
        {
            var nextYear = await controller.Preview(code, new DateOnly(2029, 11, 1), default);
            var annualPeriod = Assert.Single(Assert.IsType<SchedulePresetPeriod[]>(Assert.IsType<OkObjectResult>(nextYear.Result).Value));
            Assert.Equal(new SchedulePresetPeriod(new(2029, 2, 28), new(2029, 10, 20), new(2029, 10, 20)), annualPeriod);
        }
    }

    private sealed record Actor(Guid UserId) : ICurrentUser;
}
