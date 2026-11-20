using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportTracker.Application.Contracts;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;

namespace ReportTracker.Api.Controllers;

[ApiController, Authorize, Route("api/component-teams")]
public sealed class ComponentTeamsController(ReportTrackerDbContext db, TrackingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ComponentTeamDirectory>> Get(CancellationToken ct)
    {
        await access.EnsureActiveAsync(ct);
        if (!await access.Grants(AccessPermissions.Teams).AnyAsync(ct))
            throw new UnauthorizedAccessException("You do not have permission to manage component teams.");
        var rows = await (from component in access.ManageableComponentTeams().AsNoTracking()
                          join version in db.DefinitionVersions on component.DefinitionVersionId equals version.Id
                          join report in db.ReportDefinitions on version.ReportDefinitionId equals report.Id
                          join division in db.Divisions on component.ProviderDivisionId equals division.Id
                          orderby report.Name, component.Name, component.Id
                          select new { Component = component, ReportId = report.Id, ReportCode = report.Code, ReportName = report.Name, Division = division.Name })
            .ToListAsync(ct);
        var divisionIds = rows.Select(x => x.Component.ProviderDivisionId).Distinct().ToArray();
        var members = await db.Users.AsNoTracking().Where(x => x.IsActive && divisionIds.Contains(x.DivisionId))
            .OrderBy(x => x.DisplayName).Select(x => new ComponentTeamMember(x.Id, x.DisplayName, x.DivisionId)).ToListAsync(ct);
        return Ok(new ComponentTeamDirectory(rows.Select(x => new ComponentTeamItem(x.ReportId, x.ReportCode, x.ReportName,
            x.Division, ComponentsController.ToItem(x.Component))).ToList(), members));
    }
}
