using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
using RagnarokCompanion.Models.Api;
namespace RagnarokCompanion.Services;
public sealed class DivinePrideException:Exception {
 public HttpStatusCode? Status {get;}
 public DivinePrideException(string message,HttpStatusCode? status=null):base(message)=>Status=status;
}
public interface IDivinePrideApiClient {
 Task<List<Gateway>> GatewaysAsync(CancellationToken ct=default);
 Task<SearchResultDto> SearchAsync(string gateway,string name,int page=1,bool refresh=false,CancellationToken ct=default);
 Task<MobDto> MonsterAsync(string gateway,int id,bool refresh=false,CancellationToken ct=default);
 void ClearCache();
}
public sealed class DivinePrideApiClient:IDivinePrideApiClient,IDisposable {
 public static readonly string[] Regions={"LATAM","bRO","cRO","dpRO","idRO","GGH","GZero","iRO","jRO","kROM","kROZ","ropEU","ropRU","thROC","thROG","twRO","twROZ"};
 public static readonly string[] Languages={"pt","en","ko","ja","ru","fr","de","es","th","cn"};
 public sealed class Cached { public DateTimeOffset SavedUtc {get;set;} public MobDto Monster {get;set;}=new(); }
 readonly HttpClient http;readonly ICredentialStorageService credentials;readonly PersistenceService? store;readonly Func<string> language;readonly SemaphoreSlim gate=new(1,1);
 Dictionary<string,Cached> cached=new();DateTimeOffset blockedUntil,nextRequest;long generation;
 public DivinePrideApiClient(HttpClient http,ICredentialStorageService credentials,PersistenceService? store=null,Func<string>? language=null){this.http=http;this.credentials=credentials;this.store=store;this.language=language??(()=>"pt");if(store!=null)cached=store.Load<Dictionary<string,Cached>>("divine-pride-cache.json");}
 public static bool ValidGateway(string? region)=>Regions.Contains(region);
 public Task<List<Gateway>> GatewaysAsync(CancellationToken ct=default)=>Task.FromResult(Regions.Select(r=>new Gateway{Slug=r,Groups=new[]{"renewal"}}).ToList());
 public Task<SearchResultDto> SearchAsync(string gateway,string name,int page=1,bool refresh=false,CancellationToken ct=default)=>throw new DivinePrideException("Pesquise por nome no catálogo local e selecione um MVP para consultar seus detalhes.");
 public void ClearCache(){Interlocked.Increment(ref generation);cached=new();store?.Save("divine-pride-cache.json",cached);}
 public async Task<MobDto> MonsterAsync(string gateway,int id,bool refresh=false,CancellationToken ct=default){
  string lang=language();if(!ValidGateway(gateway)||!Languages.Contains(lang)||id<=0)throw new DivinePrideException("Selecione uma região e um idioma válidos nas Configurações.");
  string cacheId=gateway+":"+lang+":"+id;long version=Interlocked.Read(ref generation);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
  bool locked=false;
  try {
   await gate.WaitAsync(timeout.Token);locked=true;
   if(!refresh&&cached.TryGetValue(cacheId,out var hit)&&hit!=null&&hit.Monster?.Id==id&&!string.IsNullOrWhiteSpace(hit.Monster.Name)&&hit.SavedUtc<=DateTimeOffset.UtcNow&&DateTimeOffset.UtcNow-hit.SavedUtc<TimeSpan.FromDays(7))return hit.Monster;
   if(blockedUntil>DateTimeOffset.UtcNow)throw new DivinePrideException("Limite de consultas atingido. Aguarde antes de tentar novamente.",HttpStatusCode.TooManyRequests);
   if(nextRequest>DateTimeOffset.UtcNow)await Task.Delay(nextRequest-DateTimeOffset.UtcNow,timeout.Token);
   string? key;try{key=credentials.Read();}catch{throw new DivinePrideException("Não foi possível desbloquear a chave. Salve-a novamente nas Configurações.");}
   if(string.IsNullOrEmpty(key))throw new DivinePrideException("Configure sua chave Divine Pride nas Configurações.",HttpStatusCode.Unauthorized);
   using var request=new HttpRequestMessage(HttpMethod.Get,"https://www.divine-pride.net/api/database/Monster/"+id+"?apiKey="+Uri.EscapeDataString(key));
   request.Headers.Add("Accept-Language",lang);request.Headers.Add("x-server",gateway);request.Headers.Accept.ParseAdd("application/json");
   nextRequest=DateTimeOffset.UtcNow.AddSeconds(1);
   using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
   if(response.StatusCode==HttpStatusCode.TooManyRequests){var delay=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow)??TimeSpan.FromSeconds(60);blockedUntil=DateTimeOffset.UtcNow+(delay>TimeSpan.Zero?delay:TimeSpan.FromSeconds(60));throw new DivinePrideException("Limite Divine Pride atingido (429). Aguarde antes de consultar novamente.",response.StatusCode);}
   if(!response.IsSuccessStatusCode)throw new DivinePrideException(response.StatusCode switch{HttpStatusCode.Unauthorized=>"Chave Divine Pride inválida ou ausente (401).",HttpStatusCode.Forbidden=>"Acesso Divine Pride não autorizado (403).",HttpStatusCode.NotFound=>"Monstro indisponível nesta região (404).",_=>"Divine Pride indisponível. Os dados locais foram preservados."},response.StatusCode);
   await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();var block=new byte[8192];int read;
   while((read=await stream.ReadAsync(block,timeout.Token))>0){if(buffer.Length+read>2_000_000)throw new DivinePrideException("Resposta Divine Pride maior que o esperado.");buffer.Write(block,0,read);}
   using var document=JsonDocument.Parse(buffer.ToArray());var root=document.RootElement;
   if(!root.TryGetProperty("id",out var value)||!value.TryGetInt32(out int actual)||actual!=id||!root.TryGetProperty("name",out var name)||name.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(name.GetString()))throw new DivinePrideException("Resposta Divine Pride em formato inesperado.");
   bool mvp=(root.TryGetProperty("mvpDrops",out var drops)&&drops.ValueKind==JsonValueKind.Array&&drops.GetArrayLength()>0)||(root.TryGetProperty("type",out var type)&&type.GetString()=="MVP");
   var result=new MobDto{Id=id,Name=name.GetString(),GatewayId=gateway,Url="https://www.divine-pride.net/database/monster/"+id,Exp=new ExperienceDto{Mvp=mvp?1:0}};
   if(root.TryGetProperty("level",out var level)&&level.TryGetInt32(out int lv))result.Level=lv;
   if(version!=Interlocked.Read(ref generation))throw new DivinePrideException("A chave foi alterada durante a consulta. Tente novamente.");
   cached[cacheId]=new Cached{SavedUtc=DateTimeOffset.UtcNow,Monster=result};
   cached=cached.OrderByDescending(p=>p.Value?.SavedUtc).Take(200).ToDictionary(p=>p.Key,p=>p.Value);
   store?.Save("divine-pride-cache.json",cached);return result;
  }
  catch(DivinePrideException){throw;}
  catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
  catch(OperationCanceledException){throw new DivinePrideException("A consulta Divine Pride excedeu o tempo limite. Use os dados locais.");}
  catch{throw new DivinePrideException("Não foi possível consultar o Divine Pride. Seus favoritos e timers continuam disponíveis.");}
  finally{if(locked)gate.Release();}
 }
 public void Dispose(){http.Dispose();}
}