using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore;
using ReportTracker.Application.Abstractions;
using ReportTracker.Application.Contracts;
using ReportTracker.Api.Controllers;
using ReportTracker.Infrastructure.Persistence;
using ReportTracker.Infrastructure.Services;
var repo=@"D:\SEBL\Projects\GitHub\ReportTracker";
var config=JsonNode.Parse(File.ReadAllText(Path.Combine(repo,"src/ReportTracker.Api/appsettings.json")))!;
var development=JsonNode.Parse(File.ReadAllText(Path.Combine(repo,"src/ReportTracker.Api/appsettings.Development.json")))!;
var connection=development["ConnectionStrings"]?["Oracle"]?.GetValue<string>() ?? config["ConnectionStrings"]!["Oracle"]!.GetValue<string>();
await using var db=new ReportTrackerDbContext(new DbContextOptionsBuilder<ReportTrackerDbContext>().UseOracle(connection,o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
var users=await db.Users.Where(u=>u.IsActive).Select(u=>u.Id).ToListAsync();var checkedAdmin=false;
foreach(var user in users){var access=new TrackingAccess(db,new Actor(user));
 var readable=await access.AuthorizedObligations().CountAsync();var writable=await access.AuthorizedObligations(true).CountAsync();var incoming=await access.AssignedReceivingObligations().CountAsync();
 if(await access.HasAsync(AccessPermissions.Roles,null,null,default)){
  var controller=new AccessManagementController(db,access);var catalog=(AccessCatalogDirectory)((OkObjectResult)(await controller.Catalog(default)).Result!).Value!;
  if(!new[]{"SYS_ADMIN","REPORT_ADMIN","REPORT_PREPARER","COMPONENT_RECEIVER","FINAL_SUBMITTER"}.All(c=>catalog.Roles.Any(r=>r.Code==c)))throw new Exception("Missing core roles");
  foreach(var target in users){var mappingResult=(await controller.UserRoles(target,default)).Result;if(mappingResult is not OkObjectResult {Value:UserRoleMappingDirectory mapping})throw new Exception("Role mapping did not load");if(mapping.RoleIds.Distinct().Count()!=mapping.RoleIds.Count)throw new Exception("Duplicate role memberships");var result=(await controller.UserAccess(target,default)).Result;if(result is not OkObjectResult)throw new Exception("User access directory failed");}checkedAdmin=true;
 }
 if(await access.Grants(AccessPermissions.Teams).AnyAsync()){
  var response=await new ComponentTeamsController(db,access).Get(default);if(response.Result is not OkObjectResult)throw new Exception("Team directory failed");
 }
 Console.WriteLine($"PASS: scoped Oracle access queries executed; read={readable}, write={writable}, incoming={incoming}.");
}
if(!checkedAdmin)throw new Exception("No access administrator found");Console.WriteLine($"PASS: {users.Count} active accounts checked; role catalogue and user access readbacks succeeded. No data modified.");
record Actor(Guid UserId):ICurrentUser;
