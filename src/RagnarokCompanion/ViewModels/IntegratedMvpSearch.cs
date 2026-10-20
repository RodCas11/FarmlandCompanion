using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed partial class CatalogViewModel {
 readonly List<CatalogEntry> onlineEntries=new();CancellationTokenSource? selectionRequest;bool refreshingEntries,onlineBusy;
 public bool OnlineBusy {get=>onlineBusy;private set{onlineBusy=value;Changed();}}
 string onlineStatus="";public string OnlineStatus {get=>onlineStatus;private set{onlineStatus=value;Changed();}}
 public ICommand IntegratedSearchCommand {get;private set;}=null!;
 void InitializeIntegratedSearch(){IntegratedSearchCommand=new AsyncCommand(()=>Details(true));}
 void QueryChanged(){selectionRequest?.Cancel();onlineEntries.Clear();if(int.TryParse(Query,out int id)&&id>0&&!catalog.Entries.Any(e=>e.MonsterId==id))onlineEntries.Add(new CatalogEntry{Key="dp-lookup:"+id,MonsterId=id,Name="Monstro #"+id,Gateway="dp:"+GatewaySlug,IsSearchCandidate=true,Origin="Divine Pride"});RefreshEntries();OnlineStatus="Busca local por nome ou ID. Os detalhes são consultados ao selecionar.";}
 public Task SearchIntegratedAsync(bool debounce=true,bool refresh=false){QueryChanged();return Task.CompletedTask;}
 void SetAuthFailure(DivinePrideException ex){AuthStatus=ex.Message;}
 public async Task SelectOnlineAsync(CatalogEntry entry,bool favorite=false){await LoadSelectedDetailsAsync(entry);if(favorite&&SelectedEntry is {IsSearchCandidate:false} chosen&&chosen.MonsterId==entry.MonsterId)ToggleFavorite(chosen);}
 void OnSearchSelection(CatalogEntry? entry){if(refreshingEntries||entry==null||respawns.Catalog.Find(entry,true)!=null)return;selectionRequest?.Cancel();_=LoadSelectedDetailsAsync(entry);}
 async Task LoadSelectedDetailsAsync(CatalogEntry entry,bool refresh=false){
  if(entry.MonsterId<=0)return;
  selectionRequest?.Cancel();var source=new CancellationTokenSource();selectionRequest=source;OnlineBusy=true;
  try {
   if(entry.IsSearchCandidate){
    var rows=await provider.DetailsAsync(GatewaySlug,entry.MonsterId,refresh,source.Token);source.Token.ThrowIfCancellationRequested();
    rows[0].Retained=true;catalog.Merge(rows);onlineEntries.Clear();onlineEntries.AddRange(rows);catalog.SaveChoices();RefreshEntries();
    refreshingEntries=true;SelectedEntry=rows[0];refreshingEntries=false;
   }else{
    var monster=await api.MonsterAsync(GatewaySlug,entry.MonsterId,refresh,source.Token);source.Token.ThrowIfCancellationRequested();
    entry.Origin="Divine Pride — "+GatewaySlug;entry.SourceUrl=monster.Url??"https://www.divine-pride.net";entry.SyncedUtc=DateTimeOffset.UtcNow;entry.Retained=true;
    entry.Notes=monster.Name+" • Divine Pride • "+GatewaySlug+(monster.Level is {} lv?" • nível "+lv:"")+". Respawn associado ao catálogo local verificado.";
    catalog.SaveChoices();
   }
   OnlineStatus="Detalhes Divine Pride disponíveis em cache local.";AuthStatus="Conexão confirmada • "+GatewaySlug;Success("Detalhes atualizados; timers e configurações preservados.");Changed(nameof(SelectedRespawnSource));
  }catch(OperationCanceledException){}
  catch(DivinePrideException ex){if(!source.IsCancellationRequested){OnlineStatus=ex.Message;SetAuthFailure(ex);}}
  catch{if(!source.IsCancellationRequested)OnlineStatus="Não foi possível consultar o Divine Pride. Use os dados locais.";}
  finally{if(ReferenceEquals(selectionRequest,source)){selectionRequest=null;OnlineBusy=false;}source.Dispose();}
 }
}
