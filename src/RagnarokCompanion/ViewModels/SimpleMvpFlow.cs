using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class FavoriteTimerRow : Observable
{
    public CatalogEntry Entry { get; }
    readonly MainViewModel owner;
    public FavoriteTimerRow(CatalogEntry entry,MainViewModel owner){Entry=entry;this.owner=owner;}
    Mvp? Timer=>owner.Engine.Mvps.FirstOrDefault(m=>m.CatalogKey==Entry.Key && m.DeathUtc!=null);
    public string Name=>Entry.DisplayName;
    public string Countdown=>Timer is {} timer?new MvpRow(timer,owner.Engine).CompactCountdown:"—";
    public string Status=>Timer is {} timer?new MvpRow(timer,owner.Engine).Status:"Sem horário";
    public string Color=>Timer is {} timer?new MvpRow(timer,owner.Engine).StatusColor:"#9AA6BF";
    public void Refresh(){Changed(nameof(Name));Changed(nameof(Countdown));Changed(nameof(Status));Changed(nameof(Color));}
}
public sealed class CompactFavoriteChoice : Observable
{
    readonly CatalogViewModel owner;public CatalogEntry Entry {get;}
    public string Name=>Entry.DisplayName;
    public CompactFavoriteChoice(CatalogViewModel owner,CatalogEntry entry){this.owner=owner;Entry=entry;}
    public bool Chosen {get=>owner.IsCompactChosen(Entry.Key);set{owner.ChooseCompact(Entry.Key,value);Changed();}}
}
public sealed partial class CatalogViewModel
{
    Func<DateTimeOffset> simpleClock=null!;Func<string,bool> confirmSimple=null!;
    public ObservableCollection<FavoriteTimerRow> CompactFavorites {get;}=new();
    public ObservableCollection<CompactFavoriteChoice> CompactChoices {get;}=new();
    public bool NoCompactFavorites=>CompactFavorites.Count==0;
    public string DeathTitle=>SelectedEntry==null?"Selecione um MVP":"Morte de "+SelectedEntry.DisplayName;
    public string DeathTime {get;set;}="";
    public bool PreviousDay {get;set;}
    bool needsRespawn;
    public bool NeedsRespawn {get=>needsRespawn;private set{needsRespawn=value;Changed();}}
    public bool CustomCompactSelection {get=>owner.Settings.ChooseCompactFavorites;set{if(value==CustomCompactSelection)return;owner.Settings.ChooseCompactFavorites=value;if(value)owner.Settings.CompactFavoriteKeys=CompactFavorites.Select(r=>r.Entry.Key).ToList();owner.Store.Save("settings.json",owner.Settings);Changed();RefreshEntries();}}
    public ICommand ToggleFavoriteCommand {get;private set;}=null!;
    public ICommand RegisterDeathCommand {get;private set;}=null!;
    public ICommand SaveRequiredRespawnCommand {get;private set;}=null!;
    public ICommand ClearTimersCommand {get;private set;}=null!;
    public ICommand AdvancedCommand {get;private set;}=null!;
    public void PrepareSimpleView(){MapFilter="";FavoritesOnly=false;Status="";}
    void InitializeSimpleFlow(Func<DateTimeOffset>? clock,Func<string,bool>? confirm)
    {
        simpleClock=clock??(()=>DateTimeOffset.UtcNow);
        confirmSimple=confirm??(message=>MessageBox.Show(message,"Farmland Companion",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes);
        DeathTime=simpleClock().ToLocalTime().ToString("HH:mm");
        ToggleFavoriteCommand=new RelayCommand(p=>{if(p is CatalogEntry entry)Command(()=>ToggleFavorite(entry)).Execute(null);});
        RegisterDeathCommand=Command(RegisterShortDeath);
        SaveRequiredRespawnCommand=Command(()=>{if(SelectedEntry==null||RespawnMin==null||!MvpRespawnService.Valid(RespawnMin.Value,RespawnMax??RespawnMin.Value)){Status="Informe um respawn válido em minutos.";return;}SelectedEntry.CustomMin=RespawnMin;SelectedEntry.CustomMax=RespawnMax??RespawnMin;SelectedEntry.FarmlandConfirmed=ConfirmFarmland;catalog.SaveChoices();RegisterShortDeath();});
        ClearTimersCommand=Command(()=>{if(confirmSimple("Deseja limpar todos os timers ativos dos MVPs?"))ClearTimers();});
        AdvancedCommand=Command(()=>{owner.Page=3;owner.PanelCommand.Execute(null);});
    }
    void ToggleFavorite(CatalogEntry? entry)
    {
        if(entry?.IsSearchCandidate==true){_=SelectOnlineAsync(entry,true);return;}
        if(entry==null)return;entry.Favorite=!entry.Favorite;
        if(!entry.Favorite)owner.Settings.CompactFavoriteKeys.Remove(entry.Key);
        catalog.SaveChoices();owner.Store.Save("settings.json",owner.Settings);RefreshEntries();owner.Refresh();
    }
    public bool IsCompactChosen(string key)=>owner.Settings.CompactFavoriteKeys.Contains(key);
    public void ChooseCompact(string key,bool chosen)
    {
        var keys=owner.Settings.CompactFavoriteKeys;
        if(chosen&&!keys.Contains(key)){if(keys.Count>=5){Status="Escolha no máximo cinco favoritos para o compacto.";return;}keys.Add(key);}
        if(!chosen)keys.Remove(key);
        owner.Store.Save("settings.json",owner.Settings);RefreshFavoriteTimers();
    }
    void RegisterShortDeath()
    {
        var entry=SelectedEntry;if(entry==null){Status="Selecione um MVP.";return;}
        if(entry.IsSearchCandidate){Status="Aguarde a confirmação de MVP no RagnaPlace antes de registrar a morte.";return;}
        if(!MvpDeathTimeService.TryResolve(DeathTime,PreviousDay,simpleClock(),TimeZoneInfo.Local,out var death,out string error)){Status=error;return;}
        var previous=owner.Engine.Mvps.FirstOrDefault(m=>m.CatalogKey==entry.Key);
        var resolved=respawns.Resolve(entry,AcceptGateway);
        if(resolved==null&&previous!=null&&MvpRespawnService.Valid(previous.MinMinutes,previous.MaxMinutes))resolved=(previous.MinMinutes,previous.MaxMinutes,previous.Origin);
        if(resolved==null){NeedsRespawn=true;RespawnMin=null;RespawnMax=null;Changed(nameof(RespawnMin));Changed(nameof(RespawnMax));Status="Falta apenas configurar o tempo de respawn deste MVP.";return;}
        if(previous?.DeathUtc>death&&!confirmSimple("Já existe uma morte mais recente para este ponto de spawn. Deseja substituí-la pelo horário informado?"))return;
        owner.RegisterCatalogDeath(entry,resolved.Value.Min,resolved.Value.Max,resolved.Value.Origin,death);NeedsRespawn=false;RefreshFavoriteTimers();Status="Morte registrada. Próximo respawn calculado.";
    }
    public void ClearTimers()
    {
        foreach(var timer in owner.Engine.Mvps){timer.DeathUtc=null;timer.Alerts.Clear();}
        owner.Save();owner.Refresh();RefreshFavoriteTimers();Status="Timers limpos. Favoritos, respawns e histórico preservados.";
    }
    public void RefreshManualEntries()
    {
        bool changed=false;
        foreach(var timer in owner.Engine.Mvps.Where(m=>string.IsNullOrEmpty(m.CatalogKey)||m.CatalogKey.StartsWith("manual:",StringComparison.Ordinal)))
        {
            string key=string.IsNullOrEmpty(timer.CatalogKey)?"manual:"+timer.Id:timer.CatalogKey;timer.CatalogKey=key;
            var entry=catalog.Entries.FirstOrDefault(e=>e.Key==key);
            if(entry==null){entry=new CatalogEntry{Key=key,Name=timer.Name,Map=timer.Map,CustomMin=timer.MinMinutes,CustomMax=timer.MaxMinutes};catalog.Entries.Add(entry);changed=true;}
        }
        if(changed){catalog.SaveChoices();RefreshEntries();}
    }
    public void UpdateManualConfiguration(Mvp timer)
    {
        if(!string.IsNullOrEmpty(timer.CatalogKey)&&!timer.CatalogKey.StartsWith("manual:",StringComparison.Ordinal))return;
        RefreshManualEntries();var entry=catalog.Entries.First(e=>e.Key==timer.CatalogKey);
        entry.Name=timer.Name;entry.Map=timer.Map;entry.CustomMin=timer.MinMinutes;entry.CustomMax=timer.MaxMinutes;catalog.SaveChoices();RefreshEntries();
    }
    public void RefreshFavoriteTimers()
    {
        var entries=FavoriteEntries.Where(e=>!CustomCompactSelection||IsCompactChosen(e.Key))
            .OrderByDescending(e=>owner.Engine.Mvps.Any(m=>m.CatalogKey==e.Key&&m.DeathUtc!=null))
            .ThenBy(e=>owner.Engine.Mvps.FirstOrDefault(m=>m.CatalogKey==e.Key&&m.DeathUtc!=null)?.StartUtc??DateTimeOffset.MaxValue).ThenBy(e=>e.Name).Take(5).ToList();
        for(int i=0;i<entries.Count;i++)
        {
            if(i>=CompactFavorites.Count||!ReferenceEquals(CompactFavorites[i].Entry,entries[i])){var existing=CompactFavorites.FirstOrDefault(r=>ReferenceEquals(r.Entry,entries[i]));if(existing==null)CompactFavorites.Insert(i,new(entries[i],owner));else CompactFavorites.Move(CompactFavorites.IndexOf(existing),i);}
            CompactFavorites[i].Refresh();
        }
        while(CompactFavorites.Count>entries.Count)CompactFavorites.RemoveAt(CompactFavorites.Count-1);
        Changed(nameof(NoCompactFavorites));
    }
}
