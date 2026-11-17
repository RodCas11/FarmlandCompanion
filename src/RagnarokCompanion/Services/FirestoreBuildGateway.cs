using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
// Firebase ID tokens (not Google OAuth access tokens) ensure all calls use Security Rules.
public sealed partial class FirestoreBuildGateway:IBuildCommunityGateway,IDisposable
{
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(20)};public FirebaseSession Session {get;}
 public bool OrderByStars {get;set;}public string PublicAuthorName {get;set;}="";public bool Configured=>Session.Config.Configured;
 string Base=>"https://firestore.googleapis.com/v1/projects/"+Session.Config.ProjectId+"/databases/(default)/documents";
 public FirestoreBuildGateway(FirebaseSession session,HttpMessageHandler? handler=null){Session=session;if(handler!=null)http=new(handler){Timeout=TimeSpan.FromSeconds(20)};}
 static object S(string value)=>new{stringValue=value};
 static string Text(JsonElement fields,string key)=>fields.TryGetProperty(key,out var p)&&p.TryGetProperty("stringValue",out var v)?v.GetString()??"":"";
 async Task<JsonDocument> Send(HttpMethod method,string suffix,object? body,CancellationToken ct)
 {
  if(!Configured)throw new InvalidOperationException("Firebase não configurado.");
  using var request=new HttpRequestMessage(method,Base+suffix);string? token=await Session.Token(ct);if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);if(body!=null)request.Content=JsonContent.Create(body);
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);if(response.StatusCode==System.Net.HttpStatusCode.NotFound&&suffix=="/deployment/builds")throw new InvalidOperationException("Publicação ainda não liberada: falta o documento deployment/builds no Firestore. O ADM deve validar as regras e configurar publishingEnabled como true. Seu rascunho foi preservado.");if(!response.IsSuccessStatusCode)throw new InvalidOperationException(response.StatusCode==System.Net.HttpStatusCode.Forbidden?"Operação negada pelas regras do Firebase. Confira login e implantação das regras.":"Firebase indisponível ("+(int)response.StatusCode+").");
  using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new System.IO.MemoryStream();byte[] buffer=new byte[8192];int n;while((n=await stream.ReadAsync(buffer,ct))>0){if(bytes.Length+n>6_000_000)throw new InvalidOperationException("Resposta Firebase acima do limite.");bytes.Write(buffer,0,n);}return JsonDocument.Parse(bytes.ToArray());
 }
 public async Task<BuildCommunitySnapshot> FetchAsync(string classId,string stage,string purpose,CancellationToken ct)
 {
  var page=await FetchPage(classId,stage,purpose,OrderByStars,null,ct);var progress=await FetchProgress(ct);return progress with{Builds=page.Builds};
 } public async Task SubmitAsync(PlayerBuild build,CancellationToken ct)
 {
  string token=await Session.Token(ct)??throw new InvalidOperationException("Entre com Google para publicar.");
  // This server-owned marker is absent until the administrator deploys and validates rules.
  using var gate=await Send(HttpMethod.Get,"/deployment/builds",null,ct);var f=gate.RootElement.GetProperty("fields");if(!f.TryGetProperty("publishingEnabled",out var enabled)||!enabled.GetProperty("booleanValue").GetBoolean())throw new InvalidOperationException("Publicação bloqueada até o ADM validar as regras.");
  string part=token.Split('.')[1].Replace('-','+').Replace('_','/');part=part.PadRight((part.Length+3)/4*4,'=');using var claims=JsonDocument.Parse(Convert.FromBase64String(part));string uid=claims.RootElement.GetProperty("sub").GetString()!;
  string payload=JsonSerializer.Serialize(build);if(payload.Length>150000)throw new InvalidOperationException("Build acima do limite.");
  string author=BuildPublicName.Clean(PublicAuthorName.Length>0?PublicAuthorName:Session.Name);LastSubmitWasUpdate=await UpdateApproved(build,uid,payload,author,ct);if(LastSubmitWasUpdate)return;
  using var saved=await Send(HttpMethod.Patch,"/builds/"+Uri.EscapeDataString(uid+"_"+build.Id),new{fields=new Dictionary<string,object>{{"ownerUid",S(uid)},{"buildId",S(build.Id)},{"classId",S(build.ClassId)},{"stage",S(build.Stage)},{"purpose",S(build.Purpose)},{"title",S(build.Title)},{"payload",S(payload)},{"status",S("pending")},{"authorName",S(BuildPublicName.Clean(PublicAuthorName.Length>0?PublicAuthorName:Session.Name))}}},ct);
 }
 public string OwnReviewSummary {get;private set;}="";
 public string? OwnCursor {get;private set;}
 public async Task<List<PlayerBuild>> Own(CancellationToken ct,string? after=null)
 {
  string token=await Session.Token(ct)??throw new InvalidOperationException("Entre com Google.");string part=token.Split('.')[1].Replace('-','+').Replace('_','/');using var c=JsonDocument.Parse(Convert.FromBase64String(part.PadRight((part.Length+3)/4*4,'=')));string uid=c.RootElement.GetProperty("sub").GetString()!;
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{select=new{fields=new[]{"buildId","title","classId","stage","purpose","status","moderationReason"}.Select(fieldPath=>new{fieldPath})},from=new[]{new{collectionId="builds"}},where=new{fieldFilter=new{field=new{fieldPath="ownerUid"},op="EQUAL",value=S(uid)}},orderBy=new[]{new{field=new{fieldPath="__name__"},direction="ASCENDING"}},startAt=after==null?null:new{values=new[]{new{referenceValue=after}},before=false},limit=20}},ct);
  var result=new List<PlayerBuild>();var notes=new List<string>();string? last=null;foreach(var row in query.RootElement.EnumerateArray())if(row.TryGetProperty("document",out var doc)){var f=doc.GetProperty("fields");result.Add(new(){Id=Text(f,"buildId"),Title=Text(f,"title"),ClassId=Text(f,"classId"),Stage=Text(f,"stage"),Purpose=Text(f,"purpose"),Equipment=new(),Stats=new(),UpdatedUtc=DateTimeOffset.Parse(doc.GetProperty("updateTime").GetString()!)});last=doc.GetProperty("name").GetString();string status=Text(f,"status") switch{"approved"=>"Aprovada","rejected"=>"Rejeitada",_=>"Aguardando análise"};notes.Add(Text(f,"title")+": "+status+(Text(f,"moderationReason").Length>0?" — "+Text(f,"moderationReason"):""));}OwnCursor=result.Count==20?last:null;OwnReviewSummary=string.Join(Environment.NewLine,notes);return result;
 }
 public async Task<(PlayerBuild Build,DateTimeOffset Updated)> FetchOwnContent(string id,CancellationToken ct){if(await Session.Token(ct)==null)throw new InvalidOperationException("Entre com Google.");using var doc=await Send(HttpMethod.Get,"/builds/"+Uri.EscapeDataString(Session.UserId+"_"+id),null,ct);return(JsonSerializer.Deserialize<PlayerBuild>(Text(doc.RootElement.GetProperty("fields"),"payload"))??throw new InvalidOperationException("Build sem conteúdo."),DateTimeOffset.Parse(doc.RootElement.GetProperty("updateTime").GetString()!));} public void Dispose(){http.Dispose();Session.Dispose();}
}
