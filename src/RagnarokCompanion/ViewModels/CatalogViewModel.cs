using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Models.Api;
using RagnarokCompanion.Services;

namespace RagnarokCompanion.ViewModels;
public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> execute;
    private bool busy;
    public AsyncCommand(Func<Task> execute) => this.execute = execute;
    public bool CanExecute(object? p) => !busy;
    public async void Execute(object? p)
    {
        if (busy) return;
        busy = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); }
        finally { busy = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? CanExecuteChanged;
}
public sealed partial class CatalogViewModel : Observable, IDisposable
{
    private readonly MainViewModel owner;
    private readonly ICredentialStorageService credentials;
    private readonly IDivinePrideApiClient api;
    public IDivinePrideApiClient Api => api;
    private readonly IDivinePrideCatalogProvider provider;

    private readonly IMvpCatalogService catalog;
    private readonly MvpRespawnService respawns;
    public IntegrationSettings Integration { get; }
    public ObservableCollection<CatalogEntry> Entries { get; } = new();
    public ObservableCollection<CatalogEntry> FavoriteEntries { get; } = new();
    private bool respawnExpanded;
    public bool RespawnExpanded { get => respawnExpanded; set { respawnExpanded = value; Changed(); } }
    public bool HasSelection => SelectedEntry != null;
    public bool HasFavorites => FavoriteEntries.Count > 0;
    public ObservableCollection<Gateway> Gateways { get; } = new();
    public ObservableCollection<MobRefDto> RemoteResults { get; } = new();
    private string query = "", map = "", status = "Selecione um MVP para registrar o horário da morte.", authStatus = "Conexão ainda não testada";
    private bool favoritesOnly, isBusy;
    public string Query { get => query; set { if(query==value)return;query = value; owner.Search = value; Changed(); QueryChanged(); } }
    public string MapFilter { get => map; set { if(map==value)return;map = value; owner.MapFilter = value; Changed(); RefreshEntries(); } }
    public bool FavoritesOnly { get => favoritesOnly; set { if(favoritesOnly==value)return;favoritesOnly = value; Changed(); RefreshEntries(); } }
    public bool IsBusy { get => isBusy; private set { isBusy = value; Changed(); } }
    public string Status { get => status; private set { status = value; Changed(); } }
    public string AuthStatus { get => authStatus; private set { authStatus = value; Changed(); } }
    public string KeyStatus => credentials.HasKey ? "Chave armazenada com DPAPI • conteúdo oculto" : "Nenhuma chave armazenada";
    public string LastSync => Integration.LastSyncUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") ?? "Nenhuma sincronização";
    public string[] Languages=>DivinePrideApiClient.Languages;
    public string Language {get=>Integration.Language;set{if(!DivinePrideApiClient.Languages.Contains(value))return;Integration.Language=value;PersistIntegration();Changed();selectionRequest?.Cancel();}}
    public string GatewaySlug { get => Integration.Gateway; set { if(Integration.Gateway==value)return;Integration.Gateway = value ?? ""; Changed(); AuthStatus = "Região alterada • teste a conexão"; PersistIntegration(); QueryChanged(); } }
    private CatalogEntry? selectedEntry;
    public CatalogEntry? SelectedEntry
    {
        get => selectedEntry;
        set { if(ReferenceEquals(selectedEntry,value))return; selectedEntry = value; Changed(); Changed(nameof(HasSelection)); Changed(nameof(DeathTitle)); NeedsRespawn=false; RespawnMin = value?.CustomMin ?? value?.MinMinutes; RespawnMax = value?.CustomMax ?? value?.MaxMinutes; OverrideMap = value?.Map ?? ""; ConfirmFarmland = value?.FarmlandConfirmed ?? false; Changed(nameof(RespawnMin)); Changed(nameof(RespawnMax)); Changed(nameof(OverrideMap)); Changed(nameof(ConfirmFarmland)); OnSearchSelection(value); }
    }
    public CatalogEntry? SelectedFavorite { get; set; }
    public MobRefDto? SelectedRemote { get; set; }
    public double? RespawnMin { get; set; }
    public double? RespawnMax { get; set; }
    public string OverrideMap { get; set; } = "";
    public bool ConfirmFarmland { get; set; }
    public bool AcceptGateway { get; set; }
    public string ManualDeath { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    private int remotePage = 1, pages = 1;
    private string lastQuery = "", lastGateway = "";
    public string PageLabel => $"Página {remotePage}/{pages} • 10 resultados por consulta";
    public ICommand LoadGatewaysCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand DeleteKeyCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand DetailsCommand { get; }
    public ICommand RefreshDetailsCommand { get; }
    public ICommand FavoriteCommand { get; }
    public ICommand SaveRespawnCommand { get; }
    public ICommand DeathNowCommand { get; }
    public ICommand ManualDeathCommand { get; }
    public ICommand FavoriteDeathCommand { get; }
    public ICommand QuickDeathCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand SaveAndDeathCommand { get; }
    public CatalogViewModel(MainViewModel owner, IDivinePrideApiClient? api = null, ICredentialStorageService? credentials = null, Func<DateTimeOffset>? clock = null, Func<string,bool>? confirm = null)
    {
        this.owner = owner;
        respawns=new(new MvpRespawnCatalog(owner.Store));
        InitializeSimpleFlow(clock,confirm);
        this.credentials = credentials ?? new CredentialStorageService(owner.Store.Root);
        this.api = api ?? new DivinePrideApiClient(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) }, this.credentials, owner.Store,()=>Language);
        provider = new DivinePrideCatalogProvider(this.api);
        catalog = new MvpCatalogService(owner.Store);
        Integration = owner.Store.Load<IntegrationSettings>("divine-pride.json");
        if(!DivinePrideApiClient.ValidGateway(Integration.Gateway))Integration.Gateway="LATAM";
        if(!DivinePrideApiClient.Languages.Contains(Integration.Language))Integration.Language="pt";
        InitializeIntegratedSearch();
        foreach(var region in DivinePrideApiClient.Regions)Gateways.Add(new Gateway{Slug=region});
        LoadGatewaysCommand=Command(()=>Status="Regiões Divine Pride disponíveis nas Configurações.");
        TestConnectionCommand=Async(async()=>{await this.api.MonsterAsync(GatewaySlug,1039,true);AuthStatus="Conexão Divine Pride confirmada • "+GatewaySlug;Success("Conexão autenticada confirmada.");});        DeleteKeyCommand = Command(() => { this.credentials.Delete(); this.api.ClearCache(); AuthStatus = "Chave apagada"; Changed(nameof(KeyStatus)); });
        SearchCommand = Async(async () => await SearchRemote(1, Query, GatewaySlug));
        PreviousCommand = Async(async () => { if (remotePage > 1) await SearchRemote(remotePage - 1, lastQuery, lastGateway); });
        NextCommand = Async(async () => { if (remotePage < pages) await SearchRemote(remotePage + 1, lastQuery, lastGateway); });
        DetailsCommand = Async(async () => await Details(false)); RefreshDetailsCommand = Async(async () => await Details(true));
        FavoriteCommand = Command(() => ToggleFavorite(SelectedEntry));
        SaveRespawnCommand = Command(() =>
        {
            if (SelectedEntry == null || RespawnMin == null || RespawnMax == null || !MvpRespawnService.Valid(RespawnMin.Value, RespawnMax.Value)) { Status = "Selecione um MVP e informe 0 < mínimo ≤ máximo ≤ 10080 minutos."; return; }
            SelectedEntry.CustomMin = RespawnMin; SelectedEntry.CustomMax = RespawnMax; SelectedEntry.Map = OverrideMap.Trim(); SelectedEntry.FarmlandConfirmed = ConfirmFarmland;
            catalog.SaveChoices(); RefreshEntries(); Status = "Respawn personalizado salvo. Ele tem prioridade sobre os dados genéricos da API.";
        });
        DeathNowCommand = Command(() => Register(SelectedEntry, DateTimeOffset.UtcNow));
        SaveAndDeathCommand = Command(() => { SaveRespawnCommand.Execute(null); if (SelectedEntry is { } e && e.CustomMin == RespawnMin && RespawnMin != null && e.CustomMax == RespawnMax) DeathNowCommand.Execute(null); });
        ManualDeathCommand = Command(() =>
        {
            if (DateTime.TryParseExact(ManualDeath, "dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var t) && !TimeZoneInfo.Local.IsInvalidTime(t) && new DateTimeOffset(t) <= DateTimeOffset.UtcNow) Register(SelectedEntry, new DateTimeOffset(t));
            else Status = "Use data/hora local não futura: dd/MM/aaaa HH:mm:ss.";
        });
        FavoriteDeathCommand = Command(() => Register(SelectedFavorite, DateTimeOffset.UtcNow));
        QuickDeathCommand = new RelayCommand(p => { if (p is CatalogEntry e) { SelectedEntry = e; DeathNowCommand.Execute(null); } });
        BrowseCommand = Command(() => { owner.Page = 1; owner.PanelCommand.Execute(null); });
        RefreshManualEntries();RefreshEntries();ReconcileRespawns();
    }
    public void StoreKey(string key)
    {
        try { credentials.Store(key); api.ClearCache(); AuthStatus = "Chave salva • teste a conexão"; Changed(nameof(KeyStatus)); }
        catch { Status = "Não foi possível proteger a chave. Confira o valor e as permissões da pasta de dados."; }
    }
    private ICommand Async(Func<Task> action) => new AsyncCommand(async () =>
    {
        if (IsBusy) return; IsBusy = true;
        try { await action(); }
        catch (DivinePrideException ex) { Status = ex.Message; SetAuthFailure(ex); }
        catch { Status = "Não foi possível concluir a consulta. Seus timers locais foram preservados."; }
        finally { IsBusy = false; }
    });
    private ICommand Command(Action action) => new RelayCommand(_ => { try { action(); } catch { Status = "Não foi possível salvar a alteração. Os timers existentes foram preservados."; } });
    private void EnsureGateway() { if (!DivinePrideApiClient.ValidGateway(GatewaySlug)) throw new DivinePrideException("Atualize e selecione um gateway Renewal nas Configurações."); }
    private void PersistIntegration() => owner.Store.Save("divine-pride.json", Integration);
    private void Success(string message) { Status = message; Integration.LastSyncUtc = DateTimeOffset.UtcNow; PersistIntegration(); Changed(nameof(LastSync)); }
    private async Task SearchRemote(int page, string name, string gateway)
    {
        await SearchIntegratedAsync(false);
    }
    private async Task Details(bool refresh)
    {
        if(SelectedEntry==null){Status="Selecione um MVP no catálogo local.";return;}
        await LoadSelectedDetailsAsync(SelectedEntry,refresh);
    }    private void Register(CatalogEntry? entry, DateTimeOffset death)
    {
        if (entry == null) { Status = "Selecione um MVP do catálogo ou dos favoritos."; return; }
        var previous = owner.Engine.Mvps.FirstOrDefault(m => m.CatalogKey == entry.Key);
        var resolved = ResolveRegistration(entry,previous);
        // A previously adopted operational timer configuration is an explicit user choice,
        // and remains usable from favorites without retaining any raw API response.
        if (resolved == null && previous is {HasRespawn:true} && MvpRespawnService.Valid(previous.MinMinutes, previous.MaxMinutes)) resolved = (previous.MinMinutes, previous.MaxMinutes, previous.Origin);
        entry.Retained=true;catalog.SaveChoices();
        owner.RegisterCatalogDeath(entry, resolved?.Min??0, resolved?.Max??0, resolved?.Origin??"Respawn desconhecido", death,resolved!=null);
        NeedsRespawn=false;
        Status = resolved==null?"Morte salva. Respawn não encontrado para este ponto; favoritos e histórico continuam disponíveis.":"Morte registrada. O timer funciona sem novas consultas à API.";
    }
    private void RefreshEntries()
    {
        string? selectedKey = SelectedEntry?.Key, favoriteKey = SelectedFavorite?.Key;
        foreach(var group in catalog.Entries.GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase))foreach(var e in group)e.DisambiguateMap=group.Count()>1;
        refreshingEntries=true;
        var candidates=onlineEntries.Where(e=>e.IsSearchCandidate&&!catalog.Entries.Any(c=>c.MonsterId==e.MonsterId&&(c.Gateway==e.Gateway||string.IsNullOrEmpty(c.Gateway))));
        var visible=catalog.Entries.Where(e=>MvpSearchCoordinator.Matches(e,query)||onlineEntries.Any(r=>r.Key==e.Key)).Concat(candidates).DistinctBy(e=>e.Key).ToList();
        foreach(var group in visible.GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase))foreach(var e in group)e.DisambiguateMap=group.Count()>1;
        Entries.Clear(); foreach (var e in visible.Where(e => e.Map.Contains(map, StringComparison.OrdinalIgnoreCase) && (!favoritesOnly || e.Favorite)).OrderByDescending(e => e.Favorite).ThenBy(e => e.Name)) Entries.Add(e);
        FavoriteEntries.Clear(); foreach (var e in catalog.Entries.Where(e => e.Favorite).OrderBy(e => e.Name)) FavoriteEntries.Add(e);
        SelectedEntry = Entries.FirstOrDefault(e => e.Key == selectedKey);
        SelectedFavorite = FavoriteEntries.FirstOrDefault(e => e.Key == favoriteKey) ?? FavoriteEntries.FirstOrDefault(); Changed(nameof(SelectedFavorite));
        Changed(nameof(HasFavorites));
        RefreshFavoriteTimers();
        CompactChoices.Clear();foreach(var e in FavoriteEntries)CompactChoices.Add(new(this,e));
        refreshingEntries=false;
    }
    public void Dispose() { selectionRequest?.Cancel(); if (api is IDisposable d) d.Dispose(); }
}
