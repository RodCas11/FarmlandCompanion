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
public static class BuildPublicName
{
 public static string Clean(string? name){name=(name??"").Trim();return name.Length is <1 or >120||name.Any(char.IsControl)||System.Text.RegularExpressions.Regex.IsMatch(name,@"\S+@\S+\.\S+")?"Autor não informado":name;}
}
public sealed partial class FirestoreBuildGateway
{
 async Task<List<CommunityBuild>> AttachStars(List<CommunityBuild> builds,CancellationToken ct)
 {
  if(builds.Count==0||await Session.Token(ct)==null)return builds;
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{from=new[]{new{collectionId="buildStars"}},where=new{compositeFilter=new{op="AND",filters=new object[]{new{fieldFilter=new{field=new{fieldPath="ownerUid"},op="EQUAL",value=S(Session.UserId)}},new{fieldFilter=new{field=new{fieldPath="buildId"},op="IN",value=new{arrayValue=new{values=builds.Select(b=>S(b.Id)).ToArray()}}}}}}},limit=30}},ct);
  var voted=new HashSet<string>();foreach(var row in query.RootElement.EnumerateArray())if(row.TryGetProperty("document",out var d))voted.Add(Text(d.GetProperty("fields"),"buildId"));return builds.Select(b=>b with{Starred=voted.Contains(b.Id)}).ToList();
 }
 async Task<bool> HasStar(string id,CancellationToken ct){using var request=new HttpRequestMessage(HttpMethod.Get,Base+"/buildStars/"+Uri.EscapeDataString(Session.UserId+"_"+id));request.Headers.Authorization=new("Bearer",await Session.Token(ct));using var response=await http.SendAsync(request,ct);if(response.StatusCode==HttpStatusCode.NotFound)return false;response.EnsureSuccessStatusCode();return true;}
 public async Task<CommunityBuild> ToggleStar(CommunityBuild row,CancellationToken ct)
 {
  if(await Session.Token(ct)==null)throw new InvalidOperationException("Entre com Google para dar uma estrela.");
  for(int attempt=0;attempt<3;attempt++){
   using var doc=await Send(HttpMethod.Get,"/approvedBuilds/"+Uri.EscapeDataString(row.Id)+"?mask.fieldPaths=ownerUid&mask.fieldPaths=starCount",null,ct);var data=doc.RootElement;var f=data.GetProperty("fields");if(Text(f,"ownerUid")==Session.UserId)throw new InvalidOperationException("Você não pode dar estrela à própria build.");
   int count=f.TryGetProperty("starCount",out var n)?int.Parse(n.GetProperty("integerValue").GetString()!):0;bool voted=await HasStar(row.Id,ct);int next=count+(voted?-1:1);if(next<0)throw new InvalidOperationException("Contagem inconsistente. Atualize a comunidade.");
   string prefix="projects/"+Session.Config.ProjectId+"/databases/(default)/documents/";var writes=new List<object>{new{update=new{name=data.GetProperty("name").GetString(),fields=new{starCount=new{integerValue=next.ToString()}}},updateMask=new{fieldPaths=new[]{"starCount"}},currentDocument=new{updateTime=data.GetProperty("updateTime").GetString()}}};
   string star=prefix+"buildStars/"+Session.UserId+"_"+row.Id;if(voted)writes.Add(new{delete=star,currentDocument=new{exists=true}});else writes.Add(new{update=new{name=star,fields=new{ownerUid=S(Session.UserId),buildId=S(row.Id)}},currentDocument=new{exists=false}});
   try{using var saved=await Send(HttpMethod.Post,":commit",new{writes},ct);return row with{StarCount=next,Starred=!voted};}catch(InvalidOperationException)when(attempt<2){/* Re-read counts and vote after a concurrent write. */}
  }throw new InvalidOperationException("Não foi possível registrar a estrela. Tente novamente.");
 }
}
