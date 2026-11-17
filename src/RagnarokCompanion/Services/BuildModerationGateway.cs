using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed record BuildSubmission(string Path,string Updated,string Owner,PlayerBuild Build,Dictionary<string,JsonElement> Fields,string Issue="",bool HasContent=true);
public sealed partial class FirestoreBuildGateway
{
 public async Task<bool> CheckAdmin(CancellationToken ct)
 {
  if(await Session.Token(ct)==null)return false;
  using var req=new HttpRequestMessage(HttpMethod.Get,Base+"/admins/"+Uri.EscapeDataString(Session.UserId));req.Headers.Authorization=new("Bearer",await Session.Token(ct));using var response=await http.SendAsync(req,ct);
  if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)return false;response.EnsureSuccessStatusCode();return true;
 }
 public async Task<List<BuildSubmission>> Pending(CancellationToken ct,string? after=null)
 {
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{select=new{fields=new[]{"ownerUid","buildId","title","classId","stage","purpose","authorName","status"}.Select(fieldPath=>new{fieldPath})},from=new[]{new{collectionId="builds"}},where=new{fieldFilter=new{field=new{fieldPath="status"},op="EQUAL",value=S("pending")}},orderBy=new[]{new{field=new{fieldPath="__name__"},direction="ASCENDING"}},startAt=after==null?null:new{values=new[]{new{referenceValue=after}},before=false},limit=20}},ct);
  var result=query.RootElement.EnumerateArray().Where(r=>r.TryGetProperty("document",out _)).Select(r=>ParseSubmission(r.GetProperty("document"),false)).ToList();await ResolveAuthors(result.Select(r=>r.Owner),ct);foreach(var row in result)if(authorNames.TryGetValue(row.Owner,out var name)&&name.Length>0)row.Fields["authorName"]=JsonSerializer.SerializeToElement(S(name));return result;
 }
 static BuildSubmission ParseSubmission(JsonElement doc,bool full){var f=doc.GetProperty("fields");PlayerBuild? build=null;string issue="";if(full)try{build=JsonSerializer.Deserialize<PlayerBuild>(Text(f,"payload"));}catch(JsonException){issue="Dados da build inválidos.";}build??=new(){Id=Text(f,"buildId"),Title=Text(f,"title"),ClassId=Text(f,"classId"),Stage=Text(f,"stage"),Purpose=Text(f,"purpose"),Equipment=new(),Stats=new()};if(full&&(build.ClassId!=Text(f,"classId")||build.Stage!=Text(f,"stage")||build.Purpose!=Text(f,"purpose")||build.Id!=Text(f,"buildId")||build.Title!=Text(f,"title")))issue="Metadados divergentes do conteúdo enviado.";return new(doc.GetProperty("name").GetString()!,doc.GetProperty("updateTime").GetString()!,Text(f,"ownerUid"),build,f.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone()),issue,full);}
 public async Task<BuildSubmission> FetchSubmission(BuildSubmission summary,CancellationToken ct){string id=summary.Path.Split('/')[^1];using var doc=await Send(HttpMethod.Get,"/builds/"+Uri.EscapeDataString(id),null,ct);if(Text(doc.RootElement.GetProperty("fields"),"status")!="pending")throw new InvalidOperationException("Esta solicitação já foi processada. Atualize a fila.");var row=ParseSubmission(doc.RootElement,true);await ResolveAuthors(new[]{row.Owner},ct);if(authorNames.TryGetValue(row.Owner,out var name)&&name.Length>0)row.Fields["authorName"]=JsonSerializer.SerializeToElement(S(name));return row;} public async Task Moderate(BuildSubmission row,bool approve,string reason,CancellationToken ct,string adminNotes="")
 {
  // Authorization is enforced by Firestore Rules on the atomic commit.
  if(approve&&row.Issue.Length>0)throw new InvalidOperationException(row.Issue);reason=reason.Trim();if(reason.Length>500||(!approve&&reason.Length==0))throw new InvalidOperationException("Informe um motivo de rejeição (até 500 caracteres).");
  adminNotes=adminNotes.Trim();if(adminNotes.Length>2000)throw new InvalidOperationException("Notas do ADM devem ter até 2000 caracteres.");
  var fields=new Dictionary<string,object>{{"status",S(approve?"approved":"rejected")},{"moderationReason",S(reason)}};
  var writes=new List<object>{new{update=new{name=row.Path,fields},updateMask=new{fieldPaths=new[]{"status","moderationReason"}},currentDocument=new{updateTime=row.Updated}}};
  if(approve){string id=Guid.NewGuid().ToString("N");string prefix="projects/"+Session.Config.ProjectId+"/databases/(default)/documents/";var snapshot=row.Fields.Where(p=>p.Key!="moderationReason").ToDictionary(p=>p.Key,p=>(object)p.Value);snapshot["status"]=S("approved");snapshot["adminNotes"]=S(adminNotes);snapshot["starCount"]=new{integerValue="0"};snapshot["version"]=new{integerValue="1"};snapshot["updatedAt"]=S(DateTimeOffset.UtcNow.ToString("O"));writes.Add(new{update=new{name=prefix+"approvedBuilds/"+id,fields=snapshot},currentDocument=new{exists=false}});writes.Add(new{update=new{name=prefix+"approvedCombinations/"+row.Build.ClassId+":"+row.Build.Stage,fields=new Dictionary<string,object>{{"classId",S(row.Build.ClassId)},{"stage",S(row.Build.Stage)},{"approvedBuildId",S(id)}}}});}
  if(approve){using var progress=await TryDocument("communityProgress/global",ct);var keys=new HashSet<string>();bool reward=false;if(progress!=null){var p=progress.RootElement.GetProperty("fields");if(p.TryGetProperty("approvedKeys",out var array)&&array.TryGetProperty("arrayValue",out var value)&&value.TryGetProperty("values",out var values))foreach(var key in values.EnumerateArray())keys.Add(key.GetProperty("stringValue").GetString()!);reward=p.TryGetProperty("rewardUnlocked",out var flag)&&flag.GetProperty("booleanValue").GetBoolean();}keys.Add(row.Build.ClassId+":"+row.Build.Stage);var aggregate=new{name="projects/"+Session.Config.ProjectId+"/databases/(default)/documents/communityProgress/global",fields=new{approvedKeys=new{arrayValue=new{values=keys.OrderBy(k=>k).Select(k=>S(k)).ToArray()}},count=new{integerValue=keys.Count.ToString()},rewardUnlocked=new{booleanValue=reward&&keys.Count==BuildCatalog.Total}}};if(progress==null)writes.Add(new{update=aggregate,currentDocument=new{exists=false}});else writes.Add(new{update=aggregate,currentDocument=new{updateTime=progress.RootElement.GetProperty("updateTime").GetString()}});}  // Atomic commit; updateTime prevents approving a submission modified during review.
  using var committed=await Send(HttpMethod.Post,":commit",new{writes},ct);
 }
}
