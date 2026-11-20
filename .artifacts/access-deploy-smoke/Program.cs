using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Oracle.ManagedDataAccess.Client;
var repo=@"D:\SEBL\Projects\GitHub\ReportTracker";
var config=JsonNode.Parse(File.ReadAllText(Path.Combine(repo,"src/ReportTracker.Api/appsettings.json")))!;
var development=JsonNode.Parse(File.ReadAllText(Path.Combine(repo,"src/ReportTracker.Api/appsettings.Development.json")))!;
var connection=development["ConnectionStrings"]?["Oracle"]?.GetValue<string>() ?? config["ConnectionStrings"]!["Oracle"]!.GetValue<string>();
await using var db=new OracleConnection(connection);await db.OpenAsync();
var snapshots=new Dictionary<string,string>();
foreach(var table in new[]{"rt_report_definitions","rt_component_definitions","rt_tracking_obligations","rt_responsibility_assignments","rt_report_cycles"}){
 await using var cmd=db.CreateCommand();cmd.CommandText=$"SELECT * FROM \"{table}\" ORDER BY \"id\"";await using var reader=await cmd.ExecuteReaderAsync();var rows=new List<string>();
 while(await reader.ReadAsync()){var values=new List<string>();for(var i=0;i<reader.FieldCount;i++){var value=reader.GetValue(i);values.Add(value is byte[] bytes?Convert.ToHexString(bytes):value is DateTime date?date.ToString("O"):value.ToString()??"");}rows.Add(JsonSerializer.Serialize(values));}
 snapshots[table]=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',rows))));Console.WriteLine($"{table}: {rows.Count} rows");
}
// Keep the original scoped role bindings in the comparison; the upgrade adds operational roles.
await using(var cmd=db.CreateCommand()){cmd.CommandText="SELECT RAWTOHEX(\"id\"), RAWTOHEX(\"user_id\"), RAWTOHEX(\"role_id\"), RAWTOHEX(\"division_id\"), RAWTOHEX(\"report_definition_id\") FROM \"rt_user_roles\" ORDER BY \"id\"";await using var reader=await cmd.ExecuteReaderAsync();var rows=new List<string>();while(await reader.ReadAsync())rows.Add(string.Join('|',Enumerable.Range(0,reader.FieldCount).Select(i=>reader.GetValue(i).ToString())));snapshots["original_role_bindings"]=string.Join('\n',rows);}
var file=Path.Combine(repo,".artifacts/access-before.json");
if(args.Contains("--before")){File.WriteAllText(file,JsonSerializer.Serialize(snapshots));Console.WriteLine("PASS: read-only baseline saved.");}
else {var previous=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file))!;foreach(var entry in previous){if(entry.Key=="original_role_bindings"){var current=snapshots[entry.Key].Split('\n').ToHashSet();if(entry.Value.Split('\n').Any(x=>!current.Contains(x)))throw new Exception("An original scoped grant changed.");}else if(snapshots[entry.Key]!=entry.Value)throw new Exception("Operational data changed: "+entry.Key);}Console.WriteLine("PASS: operational table fingerprints and all original scoped grants are unchanged.");}
