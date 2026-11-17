using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed record SyncResult<T>(T Value,bool Cached,DateTimeOffset Updated,string Message);
public sealed class BuildCommunitySyncService
{
 readonly BuildRepository cache;readonly FirestoreBuildGateway remote;readonly SemaphoreSlim gate=new(1,1);readonly Func<bool> online;
 public BuildCommunitySyncService(BuildRepository cache,FirestoreBuildGateway remote,Func<bool>? online=null){this.cache=cache;this.remote=remote;this.online=online??NetworkInterface.GetIsNetworkAvailable;}
 public static string CatalogKey(string cls,string stage,string purpose,bool stars,string? cursor=null)=>"catalog:"+cls+":"+stage+":"+purpose+":"+stars+":"+(cursor??"first");
 public CatalogPage CachedPage(string cls,string stage,string purpose,bool stars,string? cursor=null){var state=cache.SyncState(CatalogKey(cls,stage,purpose,stars,cursor));if(state?.Data.Length>0){var page=JsonSerializer.Deserialize<CachedCatalogPage>(state.Data);if(page!=null)return new(page.Ids.Select(cache.CommunityById).Where(b=>b!=null).Select(b=>b!).ToList(),page.Cursor,page.HasMore);}return new(cache.Cached(cls,stage,purpose),null,false);}
 bool Due(BuildSyncState? state,TimeSpan age,bool manual)=>online()&&(state==null||DateTimeOffset.UtcNow>=state.RetryAfter)&&(state==null||DateTimeOffset.UtcNow-state.Updated>=(manual?TimeSpan.FromMinutes(5):age));
 void Failed(string key,BuildSyncState? prior){int failures=Math.Min(10,(prior?.Failures??0)+1);double seconds=Math.Min(1800,30*Math.Pow(2,failures-1))+Random.Shared.Next(5,25);cache.SaveSyncState(key,new(prior?.Data??"",prior?.Updated??DateTimeOffset.MinValue,DateTimeOffset.UtcNow.AddSeconds(seconds),failures),"failure/backoff");}
 public async Task<SyncResult<CatalogPage>> Catalog(string cls,string stage,string purpose,bool stars,bool manual,string? cursor,CancellationToken ct)
 {
  await gate.WaitAsync(ct);try{string key=CatalogKey(cls,stage,purpose,stars,cursor);var state=cache.SyncState(key);var local=CachedPage(cls,stage,purpose,stars,cursor);if(!Due(state,TimeSpan.FromHours(12),manual))return new(local,true,state?.Updated??DateTimeOffset.MinValue,manual?"Catálogo em cache · atualização manual a cada 5 minutos":"Catálogo local");
   try{var page=await remote.FetchPage(cls,stage,purpose,stars,cursor,ct);cache.Cache(page.Builds);if(cursor==null&&!page.HasMore)cache.ReconcileCommunity(cls,stage,purpose,page.Builds);cache.RememberStars(remote.Session.SignedIn?remote.Session.UserId:"",page.Builds);var now=DateTimeOffset.UtcNow;cache.SaveSyncState(key,new(JsonSerializer.Serialize(new CachedCatalogPage(page.Builds.Select(b=>b.Id).ToList(),page.Cursor,page.HasMore)),now,now,0),"catalog");return new(new(page.Builds.Select(b=>cache.Hydrate(b)).ToList(),page.Cursor,page.HasMore),false,now,"Catálogo sincronizado");}
   catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch{Failed(key,state);return new(local,true,state?.Updated??DateTimeOffset.MinValue,"Sem conexão ou limite atingido; usando cache. Nova tentativa com backoff.");}
  }finally{gate.Release();}
 }
 public BuildCommunitySnapshot? CachedProgress(){var state=cache.SyncState("progress");return state?.Data.Length>0?JsonSerializer.Deserialize<BuildCommunitySnapshot>(state.Data):null;}
 public async Task<SyncResult<BuildCommunitySnapshot>> Progress(bool manual,CancellationToken ct)
 {
  await gate.WaitAsync(ct);try{var state=cache.SyncState("progress");var local=CachedProgress()??new(new(),new(),false,"local");if(!Due(state,TimeSpan.FromHours(6),manual))return new(local,true,state?.Updated??DateTimeOffset.MinValue,"Progresso em cache");
   try{var snapshot=await remote.FetchProgress(ct);var now=DateTimeOffset.UtcNow;cache.SaveSyncState("progress",new(JsonSerializer.Serialize(snapshot),now,now,0),"progress");return new(snapshot,false,now,"Progresso sincronizado");}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch{Failed("progress",state);return new(local,true,state?.Updated??DateTimeOffset.MinValue,"Progresso em cache; servidor indisponível.");}
  }finally{gate.Release();}
 }
 public async Task<CommunityBuild> Content(CommunityBuild summary,CancellationToken ct)
 {
  var quick=cache.CommunityById(summary.Id);if(quick?.HasContent==true&&quick.Version>=summary.Version)return quick with{Starred=summary.Starred};if(summary.HasContent)return summary;await gate.WaitAsync(ct);try{var saved=cache.CommunityById(summary.Id);if(saved?.HasContent==true&&saved.Version>=summary.Version)return saved with{Starred=summary.Starred};
   if(!online()){var old=cache.Hydrate(summary,true);if(old.HasContent)return old;throw new InvalidOperationException("Conteúdo ainda não baixado. Conecte-se para abrir esta build pela primeira vez.");}
   string key="content:"+summary.Id;var state=cache.SyncState(key);if(state!=null&&DateTimeOffset.UtcNow<state.RetryAfter)throw new InvalidOperationException("Aguarde antes de tentar baixar esta build novamente.");
   try{var row=await remote.FetchContent(summary.Id,ct);row=row with{AuthorName=summary.AuthorName.Length>0?summary.AuthorName:row.AuthorName,Starred=summary.Starred};cache.Cache(new[]{row});var now=DateTimeOffset.UtcNow;cache.SaveSyncState(key,new(row.Version.ToString(),now,now,0),"content");return row;}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch{Failed(key,state);var old=cache.Hydrate(summary,true);if(old.HasContent)return old;throw;}
  }finally{gate.Release();}
 }
 async Task<T> PrivateContent<T>(string key,DateTimeOffset version,Func<Task<(T Value,DateTimeOffset Updated)>> fetch,CancellationToken ct) where T:class {
  var state=cache.SyncState(key);if(state?.Data.Length>0&&(state.Updated>=version||!online()))return JsonSerializer.Deserialize<T>(state.Data)!;
  await gate.WaitAsync(ct);try{state=cache.SyncState(key);if(state?.Data.Length>0&&state.Updated>=version)return JsonSerializer.Deserialize<T>(state.Data)!;if(!online()||state!=null&&DateTimeOffset.UtcNow<state.RetryAfter){if(state?.Data.Length>0)return JsonSerializer.Deserialize<T>(state.Data)!;throw new InvalidOperationException("Conteúdo não disponível offline ou aguardando nova tentativa.");}try{var result=await fetch();var now=DateTimeOffset.UtcNow;cache.SaveSyncState(key,new(JsonSerializer.Serialize(result.Value),result.Updated,now,0),"private-content");return result.Value;}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch{Failed(key,state);if(state?.Data.Length>0)return JsonSerializer.Deserialize<T>(state.Data)!;throw;}}finally{gate.Release();}
 }
 public Task<PlayerBuild> OwnContent(PlayerBuild summary,CancellationToken ct)=>summary.Equipment.Count>0||summary.Stats.Count>0?Task.FromResult(summary):PrivateContent("own-content:"+remote.Session.UserId+":"+summary.Id,summary.UpdatedUtc,async()=>{var result=await remote.FetchOwnContent(summary.Id,ct);return(result.Build,result.Updated);},ct);
 public Task<BuildSubmission> SubmissionContent(BuildSubmission summary,CancellationToken ct)=>summary.HasContent?Task.FromResult(summary):PrivateContent("admin-content:"+remote.Session.UserId+":"+summary.Path,DateTimeOffset.Parse(summary.Updated),async()=>{var row=await remote.FetchSubmission(summary,ct);return(row,DateTimeOffset.Parse(row.Updated));},ct);
}