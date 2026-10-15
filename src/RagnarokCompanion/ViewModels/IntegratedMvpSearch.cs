using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;

namespace RagnarokCompanion.ViewModels;
public sealed partial class CatalogViewModel
{
    MvpSearchCoordinator? searchCoordinator;
    readonly List<CatalogEntry> onlineEntries=new();
    CancellationTokenSource? selectionRequest;
    long searchVersion;
    bool onlineBusy, selectionBusy, refreshingEntries;
    string onlineStatus="";
    public bool OnlineBusy {get=>onlineBusy||selectionBusy;private set{onlineBusy=value;Changed();}}
    public string OnlineStatus {get=>onlineStatus;private set{onlineStatus=value;Changed();}}
    public ICommand IntegratedSearchCommand {get;private set;}=null!;
    void InitializeIntegratedSearch()
    {
        searchCoordinator=new(api);
        IntegratedSearchCommand=new AsyncCommand(()=>SearchIntegratedAsync(false,true));
    }
    void QueryChanged()
    {
        onlineEntries.Clear(); selectionRequest?.Cancel();
        RefreshEntries();
        _=SearchIntegratedAsync();
    }
    public async Task SearchIntegratedAsync(bool debounce=true,bool refresh=false)
    {
        if(searchCoordinator==null)return;
        long version=++searchVersion;
        searchCoordinator.Cancel();
        if(string.IsNullOrWhiteSpace(Query)){OnlineBusy=false;OnlineStatus="";return;}
        if(!credentials.HasKey||!RagnaPlaceApiClient.ValidGateway(GatewaySlug)){
            OnlineBusy=false;OnlineStatus="Busca local. Configure sua chave e um gateway Renewal para consultar o RagnaPlace.";return;
        }
        OnlineBusy=true;OnlineStatus="Consultando RagnaPlace…";
        try {
            var result=await searchCoordinator.SearchAsync(GatewaySlug,Query,refresh:refresh,debounce:debounce);
            if(version!=searchVersion||result==null)return;
            onlineEntries.Clear();onlineEntries.AddRange(result);
            foreach(var entry in result.Where(e=>!e.IsSearchCandidate))catalog.Merge(new[]{entry});
            RefreshEntries();AuthStatus="Consulta autenticada confirmada • "+GatewaySlug;Success("Consulta RagnaPlace concluída.");
            OnlineStatus=result.Count==0?"Nenhum MVP encontrado no RagnaPlace para esta busca.":"RagnaPlace: até 10 resultados por consulta. Selecione para confirmar MVP e consultar mapas; refine o nome para encontrar outros.";
        }
        catch(RagnaPlaceException ex){if(version==searchVersion){OnlineStatus="Não foi possível consultar o RagnaPlace. Você ainda pode usar os MVPs salvos. "+ex.Message;SetAuthFailure(ex);}}
        catch {if(version==searchVersion)OnlineStatus="Não foi possível consultar o RagnaPlace. Você ainda pode usar os MVPs salvos.";}
        finally {if(version==searchVersion)OnlineBusy=false;}
    }
    void SetAuthFailure(RagnaPlaceException ex)
    {
        AuthStatus=ex.Status switch {HttpStatusCode.Unauthorized=>"Chave inválida/ausente (401)",HttpStatusCode.Forbidden=>"Sem autorização (403)",HttpStatusCode.TooManyRequests=>"Limite de consultas atingido (HTTP 429)",_=>ex.Message};
    }
    public async Task SelectOnlineAsync(CatalogEntry entry,bool favorite=false)
    {
        if(!entry.IsSearchCandidate){if(favorite)ToggleFavorite(entry);return;}
        selectionRequest?.Cancel();var source=new CancellationTokenSource();selectionRequest=source;
        selectionBusy=true;Changed(nameof(OnlineBusy));OnlineStatus="Confirmando MVP no RagnaPlace…";
        try {
            var details=await provider.DetailsAsync(entry.Gateway,entry.MonsterId,ct:source.Token);
            source.Token.ThrowIfCancellationRequested();
            var chosen=details.First();chosen.Retained=true;
            catalog.Merge(details);onlineEntries.RemoveAll(e=>e.MonsterId==entry.MonsterId&&e.Gateway==entry.Gateway);onlineEntries.AddRange(details);
            catalog.SaveChoices();RefreshEntries();SelectedEntry=chosen;
            if(favorite)ToggleFavorite(chosen);
            Success("MVP confirmado por exp.mvp; escolha o horário da morte. Os tempos do Farmland continuam personalizados.");OnlineStatus="MVP confirmado e salvo para uso offline.";
        }
        catch(Exception) when(source.IsCancellationRequested){}
        catch(RagnaPlaceException ex){OnlineStatus=ex.Message;SetAuthFailure(ex);}
        catch {OnlineStatus="Não foi possível consultar o RagnaPlace. Você ainda pode usar os MVPs salvos.";}
        finally {if(ReferenceEquals(selectionRequest,source)){selectionRequest=null;selectionBusy=false;Changed(nameof(OnlineBusy));}source.Dispose();}
    }
    void OnSearchSelection(CatalogEntry? entry)
    {
        if(refreshingEntries||entry==null)return;
        selectionRequest?.Cancel();
        if(entry.IsSearchCandidate)_=SelectOnlineAsync(entry);
        else if(!string.IsNullOrEmpty(entry.Gateway)){entry.Retained=true;try{catalog.SaveChoices();}catch{Status="Não foi possível salvar a seleção.";}}
    }
}
