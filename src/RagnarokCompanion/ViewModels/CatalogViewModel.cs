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
    private readonly IRagnaPlaceApiClient api;
    private readonly IRagnaPlaceCatalogProvider provider;
    private readonly IRagnaPlaceAuthService auth;
    private readonly IMvpCatalogService catalog;
    private readonly IMvpRespawnService respawns = new MvpRespawnService();
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
    public string Query { get => query; set { query = value; owner.Search = value; Changed(); RefreshEntries(); } }
    public string MapFilter { get => map; set { if(map==value)return;map = value; owner.MapFilter = value; Changed(); RefreshEntries(); } }
    public bool FavoritesOnly { get => favoritesOnly; set { if(favoritesOnly==value)return;favoritesOnly = value; Changed(); RefreshEntries(); } }
    public bool IsBusy { get => isBusy; private set { isBusy = value; Changed(); } }
    public string Status { get => status; private set { status = value; Changed(); } }
    public string AuthStatus { get => authStatus; private set { authStatus = value; Changed(); } }
    public string KeyStatus => credentials.HasKey ? "Chave armazenada com DPAPI • conteúdo oculto" : "Nenhuma chave armazenada";
    public string LastSync => Integration.LastSyncUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") ?? "Nenhuma sincronização";
    public string GatewaySlug { get => Integration.Gateway; set { Integration.Gateway = value ?? ""; Changed(); AuthStatus = "Gateway alterado • teste a conexão"; PersistIntegration(); } }
    private CatalogEntry? selectedEntry;
    public CatalogEntry? SelectedEntry
    {
        get => selectedEntry;
        set { if(ReferenceEquals(selectedEntry,value))return; selectedEntry = value; Changed(); Changed(nameof(HasSelection)); Changed(nameof(DeathTitle)); NeedsRespawn=false; RespawnMin = value?.CustomMin ?? value?.MinMinutes; RespawnMax = value?.CustomMax ?? value?.MaxMinutes; OverrideMap = value?.Map ?? ""; ConfirmFarmland = value?.FarmlandConfirmed ?? false; Changed(nameof(RespawnMin)); Changed(nameof(RespawnMax)); Changed(nameof(OverrideMap)); Changed(nameof(ConfirmFarmland)); }
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
    public CatalogViewModel(MainViewModel owner, IRagnaPlaceApiClient? api = null, ICredentialStorageService? credentials = null, Func<DateTimeOffset>? clock = null, Func<string,bool>? confirm = null)
    {
        this.owner = owner;
        InitializeSimpleFlow(clock,confirm);
        this.credentials = credentials ?? new CredentialStorageService(owner.Store.Root);
        this.api = api ?? new RagnaPlaceApiClient(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) }, this.credentials, new LocalCacheService());
        provider = new RagnaPlaceCatalogProvider(this.api); auth = new RagnaPlaceAuthService(this.api);
        catalog = new MvpCatalogService(owner.Store);
        Integration = owner.Store.Load<IntegrationSettings>("ragnaplace.json");
        // No unauthenticated guess is made about renewal gateways on startup.
        if (!string.IsNullOrEmpty(Integration.Gateway)) Gateways.Add(new Gateway { Slug = Integration.Gateway, Groups = new[] { "renewal" } });
        LoadGatewaysCommand = Async(async () =>
        {
            var gateways = await this.api.GatewaysAsync(); Gateways.Clear(); foreach (var g in gateways) Gateways.Add(g);
            if (!gateways.Any(g => g.Slug == GatewaySlug)) GatewaySlug = gateways.FirstOrDefault()?.Slug ?? "";
            Success("Gateways Renewal ativos atualizados. Nenhum gateway oficial representa o Farmland.");
        });
        TestConnectionCommand = Async(async () => { EnsureGateway(); await auth.TestAsync(GatewaySlug); AuthStatus = "Autenticação confirmada • " + GatewaySlug; Success("Conexão autenticada confirmada com o RagnaPlace."); });
        DeleteKeyCommand = Command(() => { this.credentials.Delete(); this.api.ClearCache(); AuthStatus = "Chave apagada"; Changed(nameof(KeyStatus)); });
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
        RefreshManualEntries();RefreshEntries();
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
        catch (RagnaPlaceException ex) { Status = ex.Message; if (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) AuthStatus = ex.Status == HttpStatusCode.Unauthorized ? "Chave inválida/ausente (401)" : "Sem autorização (403)"; }
        catch { Status = "Não foi possível concluir a consulta. Seus timers locais foram preservados."; }
        finally { IsBusy = false; }
    });
    private ICommand Command(Action action) => new RelayCommand(_ => { try { action(); } catch { Status = "Não foi possível salvar a alteração. Os timers existentes foram preservados."; } });
    private void EnsureGateway() { if (!RagnaPlaceApiClient.ValidGateway(GatewaySlug)) throw new RagnaPlaceException("Atualize e selecione um gateway Renewal nas Configurações."); }
    private void PersistIntegration() => owner.Store.Save("ragnaplace.json", Integration);
    private void Success(string message) { Status = message; Integration.LastSyncUtc = DateTimeOffset.UtcNow; PersistIntegration(); Changed(nameof(LastSync)); }
    private async Task SearchRemote(int page, string name, string gateway)
    {
        EnsureGateway(); var r = await provider.SearchAsync(gateway, name, page);
        RemoteResults.Clear(); foreach (var item in r.Items!) RemoteResults.Add(item);
        lastQuery = name; lastGateway = gateway; remotePage = page;
        pages = Math.Clamp(r.Pages ?? 1, 1, 20); Changed(nameof(PageLabel));
        Success("Resultados de monstros. Consulte os detalhes para confirmar se são MVPs; a busca não oferece filtro MVP documentado.");
    }
    private async Task Details(bool refresh)
    {
        EnsureGateway(); int? id = refresh ? SelectedEntry?.MonsterId ?? SelectedRemote?.Id : SelectedRemote?.Id ?? SelectedEntry?.MonsterId;
        if (id == null || id <= 0) { Status = "Selecione um resultado RagnaPlace ou um MVP do catálogo online."; return; }
        var entries = await provider.DetailsAsync(GatewaySlug, id.Value, refresh); catalog.Merge(entries); RefreshEntries(); SelectedEntry = entries.FirstOrDefault();
        Success("Detalhes RagnaPlace carregados. Cada mapa é uma entrada independente; confirme os tempos no Farmland.");
    }
    private void Register(CatalogEntry? entry, DateTimeOffset death)
    {
        if (entry == null) { Status = "Selecione um MVP do catálogo ou dos favoritos."; return; }
        var resolved = respawns.Resolve(entry, AcceptGateway);
        // A previously adopted operational timer configuration is an explicit user choice,
        // and remains usable from favorites without retaining any raw API response.
        var previous = owner.Engine.Mvps.FirstOrDefault(m => m.CatalogKey == entry.Key);
        if (resolved == null && previous != null && MvpRespawnService.Valid(previous.MinMinutes, previous.MaxMinutes)) resolved = (previous.MinMinutes, previous.MaxMinutes, previous.Origin);
        if (resolved == null) { SelectedEntry = entry; RespawnExpanded = true; NeedsRespawn=true; owner.Page = 1; owner.PanelCommand.Execute(null); Status = "Falta só o tempo de respawn. Preencha e salve abaixo; nome e mapa já estão selecionados."; return; }
        owner.RegisterCatalogDeath(entry, resolved.Value.Min, resolved.Value.Max, resolved.Value.Origin, death);
        NeedsRespawn=false;
        Status = "Morte registrada. O timer funciona sem novas consultas à API.";
    }
    private void RefreshEntries()
    {
        string? selectedKey = SelectedEntry?.Key, favoriteKey = SelectedFavorite?.Key;
        foreach(var group in catalog.Entries.GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase))foreach(var e in group)e.DisambiguateMap=group.Count()>1;
        Entries.Clear(); foreach (var e in catalog.Entries.Where(e => CultureInfo.InvariantCulture.CompareInfo.IndexOf(e.Name,query.Trim(),CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0 && e.Map.Contains(map, StringComparison.OrdinalIgnoreCase) && (!favoritesOnly || e.Favorite)).OrderByDescending(e => e.Favorite).ThenBy(e => e.Name)) Entries.Add(e);
        FavoriteEntries.Clear(); foreach (var e in catalog.Entries.Where(e => e.Favorite).OrderBy(e => e.Name)) FavoriteEntries.Add(e);
        SelectedEntry = Entries.FirstOrDefault(e => e.Key == selectedKey);
        SelectedFavorite = FavoriteEntries.FirstOrDefault(e => e.Key == favoriteKey) ?? FavoriteEntries.FirstOrDefault(); Changed(nameof(SelectedFavorite));
        Changed(nameof(HasFavorites));
        RefreshFavoriteTimers();
        CompactChoices.Clear();foreach(var e in FavoriteEntries)CompactChoices.Add(new(this,e));
    }
    public void Dispose() { if (api is IDisposable d) d.Dispose(); }
}
