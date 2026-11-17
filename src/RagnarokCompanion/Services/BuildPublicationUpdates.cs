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
public sealed record CommunityProfile(string Name,string DiscordNickname);
public sealed partial class FirestoreBuildGateway
{
 readonly Dictionary<string,string> authorNames=new();DateTimeOffset authorsRead;
 public bool LastSubmitWasUpdate {get;private set;}
 async Task<JsonDocument?> TryDocument(string path,CancellationToken ct){using var request=new HttpRequestMessage(HttpMethod.Get,Base+"/"+path);var token=await Session.Token(ct);if(token!=null)request.Headers.Authorization=new("Bearer",token);using var response=await http.SendAsync(request,ct);if(response.StatusCode==HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));}
 public async Task<CommunityProfile> SavePublicProfile(string nickname,bool force,CancellationToken ct)
 {
  if(await Session.Token(ct)==null)throw new InvalidOperationException("Entre com Google.");string uid=Session.UserId;
  using var current=await TryDocument("communityProfiles/"+Uri.EscapeDataString(uid),ct);
  string remoteNick=current==null?"":Text(current.RootElement.GetProperty("fields"),"discordNickname");
  if(!force&&nickname.Length==0&&remoteNick.Length>0)nickname=remoteNick;
  string name=BuildPublicName.Clean(nickname.Length>0?nickname:Session.Name);
  if(current==null||Text(current.RootElement.GetProperty("fields"),"displayName")!=name||remoteNick!=nickname){using var saved=await Send(HttpMethod.Patch,"/communityProfiles/"+Uri.EscapeDataString(uid),new{fields=new{displayName=S(name),discordNickname=S(nickname)}},ct);}
  PublicAuthorName=name;authorNames[uid]=name;return new(name,nickname);
 }
 async Task ResolveAuthors(IEnumerable<string> uids,CancellationToken ct)
 {
  if(DateTimeOffset.UtcNow-authorsRead>TimeSpan.FromMinutes(1)){authorNames.Clear();authorsRead=DateTimeOffset.UtcNow;}var missing=uids.Where(s=>!string.IsNullOrWhiteSpace(s)&&!authorNames.ContainsKey(s)).Distinct().Take(30).ToArray();if(missing.Length==0)return;
  string prefix="projects/"+Session.Config.ProjectId+"/databases/(default)/documents/communityProfiles/";
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{from=new[]{new{collectionId="communityProfiles"}},where=new{fieldFilter=new{field=new{fieldPath="__name__"},op="IN",value=new{arrayValue=new{values=missing.Select(uid=>new{referenceValue=prefix+uid}).ToArray()}}}},limit=30}},ct);
  foreach(string uid in missing)authorNames[uid]="";
  foreach(var row in query.RootElement.EnumerateArray())if(row.TryGetProperty("document",out var doc)){string uid=doc.GetProperty("name").GetString()!.Split('/')[^1];authorNames[uid]=Text(doc.GetProperty("fields"),"displayName");}
 }
 async Task<List<CommunityBuild>> WithAuthors(List<CommunityBuild> rows,CancellationToken ct){await ResolveAuthors(rows.Where(r=>string.IsNullOrWhiteSpace(r.AuthorName)||r.AuthorName=="Autor não informado").Select(r=>r.OwnerUid),ct);return rows.Select(r=>r with{AuthorName=authorNames.TryGetValue(r.OwnerUid,out var name)&&name.Length>0?name:r.AuthorName}).ToList();}
 async Task<bool> UpdateApproved(PlayerBuild build,string uid,string payload,string author,CancellationToken ct)
 {
  var filters=new[]{("ownerUid",uid),("buildId",build.Id)}.Select(f=>new{fieldFilter=new{field=new{fieldPath=f.Item1},op="EQUAL",value=S(f.Item2)}});
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{from=new[]{new{collectionId="approvedBuilds"}},where=new{compositeFilter=new{op="AND",filters}},limit=30}},ct);
  var docs=query.RootElement.EnumerateArray().Where(r=>r.TryGetProperty("document",out _)).Select(r=>r.GetProperty("document")).ToArray();if(docs.Length==0)return false;
  var matching=docs.Where(d=>Text(d.GetProperty("fields"),"classId")==build.ClassId&&Text(d.GetProperty("fields"),"stage")==build.Stage).ToArray();if(matching.Length==0)throw new InvalidOperationException("Para mudar classe ou estágio de uma build aprovada, crie uma nova build.");
  var fields=new Dictionary<string,object>{{"payload",S(payload)},{"title",S(build.Title)},{"purpose",S(build.Purpose)},{"authorName",S(author)}};
  var writes=new List<object>();foreach(var doc in matching){var versioned=new Dictionary<string,object>(fields){{"version",new{integerValue=(Number(doc.GetProperty("fields"),"version",1)+1).ToString()}},{"updatedAt",S(DateTimeOffset.UtcNow.ToString("O"))}};writes.Add(new{update=new{name=doc.GetProperty("name").GetString(),fields=versioned},updateMask=new{fieldPaths=versioned.Keys.ToArray()},currentDocument=new{updateTime=doc.GetProperty("updateTime").GetString()}});}
  string approvedId=matching[0].GetProperty("name").GetString()!.Split('/')[^1];
  var submission=new Dictionary<string,object>{{"ownerUid",S(uid)},{"buildId",S(build.Id)},{"classId",S(build.ClassId)},{"stage",S(build.Stage)},{"purpose",S(build.Purpose)},{"title",S(build.Title)},{"payload",S(payload)},{"authorName",S(author)},{"status",S("approved")},{"approvedBuildId",S(approvedId)}};
  writes.Add(new{update=new{name="projects/"+Session.Config.ProjectId+"/databases/(default)/documents/builds/"+uid+"_"+build.Id,fields=submission}});
  using var committed=await Send(HttpMethod.Post,":commit",new{writes},ct);return true;
 }
}
