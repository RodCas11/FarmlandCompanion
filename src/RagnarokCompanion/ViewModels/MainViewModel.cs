using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;

namespace RagnarokCompanion.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> execute;
    public RelayCommand(Action<object?> action) => execute = action;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class MvpRow : Observable
{
    public Mvp Model { get; }
    private readonly TimerService engine;
    public MvpRow(Mvp model, TimerService service) { Model = model; engine = service; }
    public string Name => Model.Name;
    public string Map => Model.Map;
    public string Notes => Model.Notes;
    public string Origin => Model.Origin;
    public string Status => Model.StartUtc > DateTimeOffset.UtcNow && Model.StartUtc <= DateTimeOffset.UtcNow.AddMinutes(10) ? "Próximo de nascer" : engine.Status(Model, DateTimeOffset.UtcNow);
    public string StatusColor => Model.StartUtc <= DateTimeOffset.UtcNow ? "#FF8797" : Model.StartUtc <= DateTimeOffset.UtcNow.AddMinutes(10) ? "#FFBD70" : "#9AA6BF";
    public string Countdown => Model.StartUtc == null ? "—" : DateTimeOffset.UtcNow < Model.StartUtc ? FormatRespawn(Model.StartUtc.Value - DateTimeOffset.UtcNow) : DateTimeOffset.UtcNow < Model.EndUtc ? "Janela • " + FormatRespawn(Model.EndUtc.Value - DateTimeOffset.UtcNow) : "Janela encerrada";
    public string CompactCountdown => Model.StartUtc == null ? "—" : FormatRespawn((DateTimeOffset.UtcNow < Model.StartUtc ? Model.StartUtc.Value : Model.EndUtc!.Value) - DateTimeOffset.UtcNow);
    public string Window => Model.StartUtc == null ? Model.DeathUtc!=null?"Morte salva • respawn desconhecido":"Registre uma morte para monitorar" : Model.StartUtc == Model.EndUtc ? Local(Model.StartUtc) : Local(Model.StartUtc) + " → " + Local(Model.EndUtc);
    static string FormatRespawn(TimeSpan remaining){var t=TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0,remaining.TotalSeconds)));return t.TotalHours>=1?$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}":TimerService.Format(t);}
    public string LastDeath => Local(Model.DeathUtc);
    public static string Local(DateTimeOffset? date) => date?.ToLocalTime().ToString("dd/MM HH:mm:ss") ?? "—";
    public void Refresh() { foreach (var n in new[] { nameof(Name), nameof(Map), nameof(Notes), nameof(Origin), nameof(Status), nameof(StatusColor), nameof(Countdown), nameof(CompactCountdown), nameof(Window), nameof(LastDeath) }) Changed(n); }
}
public sealed partial class MainViewModel : Observable
{
    public CatalogViewModel Catalog { get; }
    public SpotlightViewModel Spotlight { get; }
    public EventViewModel Events { get; }
    public TimerService Engine { get; }
    public PersistenceService Store { get; }
    public Settings Settings => Engine.Settings;
    public WindowLayout Layout { get; }
    public ObservableCollection<MvpRow> Rows { get; } = new();
    public ObservableCollection<MvpRow> NextRows { get; } = new();
    public ObservableCollection<MvpRow> CompactRows { get; } = new();
    public ObservableCollection<UpcomingEvent> UpcomingEvents { get; } = new();
    public bool NoEvents => UpcomingEvents.Count == 0;
    public bool NoActiveMvps => NextRows.Count == 0;
    public bool MiningAvailable => Engine.Phase(DateTimeOffset.UtcNow) == MiningPhase.Available;
    public string MiningColor => Engine.Phase(DateTimeOffset.UtcNow) switch { MiningPhase.Available => "#6CD9A5", MiningPhase.Mining => "#70C7FF", _ => "#FFBD70" };
    public string MiningNext => MiningAvailable ? "Você já pode entrar na mina." : "Próxima entrada • " + Ready;
    private bool editorExpanded;
    public bool EditorExpanded { get => editorExpanded; set { editorExpanded = value; Changed(); } }
    public ObservableCollection<HistoryEntry> History { get; } = new();
    public ObservableCollection<HistoryEntry> RecentAlerts { get; } = new();
    public event Action? OpenCompact;
    public event Action? OpenPanel;
    public event Action? SettingsChanged;
    public ICommand StartMiningCommand { get; }
    public ICommand ResetMiningCommand { get; }
    public ICommand CorrectMiningCommand { get; }
    public ICommand NewMvpCommand { get; }
    public ICommand EditMvpCommand { get; }
    public ICommand DeleteMvpCommand { get; }
    public ICommand KillCommand { get; }
    public ICommand CorrectDeathCommand { get; }
    public ICommand SaveMvpCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CompactCommand { get; }
    public ICommand PanelCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand ExpandCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand NavigateCommand { get; }
    private int page;
    public int Page { get => page; set { page = value; Changed(); } }
    private string search = "", mapFilter = "";
    public string Search { get => search; set { search = value; Changed(); RefreshLists(); } }
    public string MapFilter { get => mapFilter; set { mapFilter = value; Changed(); RefreshLists(); } }
    private MvpRow? selectedMvp;
    public MvpRow? SelectedMvp { get => selectedMvp; set { selectedMvp = value; Changed(); } }
    private Mvp? editing;
    public string EditName { get; set; } = "";
    public string EditMap { get; set; } = "";
    public double EditMin { get; set; } = 60;
    public double EditMax { get; set; } = 60;
    public string EditNotes { get; set; } = "";
    public string ManualDeath { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    public string MiningEntry { get; set; } = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    private bool expanded;
    public bool ShowMining => Settings.CompactMode != "MVPs";
    public string ExpandLabel => expanded ? "Recolher" : "Ver todos";
    public string MiningStatus => Engine.Phase(DateTimeOffset.UtcNow) switch { MiningPhase.Mining => "Minerando", MiningPhase.Cooldown => "Cooldown", _ => "Disponível" };
    public string MiningCountdown => TimerService.Format(Engine.Remaining(DateTimeOffset.UtcNow));
    public string MiningSummary => "MINA • " + MiningStatus.ToUpperInvariant() + "  " + MiningCountdown;
    public double MiningProgress => Engine.Progress(DateTimeOffset.UtcNow);
    public string Entered => MvpRow.Local(Engine.Mining.EnteredUtc);
    public string Exit => MvpRow.Local(Engine.Mining.ExitUtc);
    public string Ready => MvpRow.Local(Engine.Mining.ReadyUtc);
    public string Count => Engine.Mvps.Count + " MVPs cadastrados • " + Engine.Mvps.Count(m => m.DeathUtc != null) + " monitorados";
    public string CompactEmpty => Settings.CompactMode != "Mineração" && CompactRows.Count == 0 ? "Nenhum MVP monitorado" : "";
    public bool NoCompactMvps => Settings.CompactMode != "Mineração" && CompactRows.Count == 0;
    public string PinLabel => Settings.Topmost ? "● Fixado" : "○ Fixar";
    public string Feedback { get; private set; } = "Pronto para acompanhar sua próxima caçada.";
    private int lastHistoryCount = -1;
    public MainViewModel(PersistenceService store, Func<DateTimeOffset>? mvpClock=null, Func<string,bool>? confirmMvp=null, IRagnaPlaceApiClient? mvpApi=null, ICredentialStorageService? mvpCredentials=null)
    {
        Store = store;
        var settings = store.Load<Settings>("settings.json"); PersistenceService.Validate(settings);
        var mining = store.Load<MiningState>("mining.json");
        if (mining.EnteredUtc != null && (mining.ExitUtc == null || mining.ReadyUtc == null || mining.ExitUtc <= mining.EnteredUtc || mining.ReadyUtc <= mining.ExitUtc))
        { mining = new(); store.Warnings.Add("Estado da mina inválido; ciclo restaurado como disponível."); }
        mining.Alerts ??= new();
        var mvps = store.Load<List<Mvp>>("mvps.json");
        int removed = mvps.RemoveAll(m => !PersistenceService.Valid(m));
        if (removed > 0) store.Warnings.Add($"{removed} cadastro(s) inválido(s) de MVP ignorado(s).");
        mvps = mvps.GroupBy(m => m.Id).Select(g => g.First()).ToList();
        var history = store.Load<List<HistoryEntry>>("history.json");
        history.RemoveAll(h => h == null || h.Message == null || h.Kind == null);
        Engine = new(settings, mining, mvps, history);
        Catalog = new CatalogViewModel(this,api:mvpApi,credentials:mvpCredentials,clock:mvpClock,confirm:confirmMvp);
        Spotlight = new SpotlightViewModel(store);
        Events = new EventViewModel(new EventRepository(store.IsDefaultLocation ? null : Path.Combine(store.Root, "events")));
        Layout = store.Load<WindowLayout>("window-layout.json");
        InitializeCompactTabs();
        StartMiningCommand = Cmd(_ => { if (Engine.Phase(DateTimeOffset.UtcNow) != MiningPhase.Available) { Tell("A mina está em um ciclo. Use corrigir ou reiniciar."); return; } Engine.StartMining(DateTimeOffset.UtcNow); Commit(); AutoCompact(); });
        ResetMiningCommand = Cmd(_ => { Engine.ResetMining(DateTimeOffset.UtcNow); Commit(); });
        CorrectMiningCommand = Cmd(_ => { if (TryLocal(MiningEntry, out var t)) { Engine.StartMining(t); Commit(); } });
        NewMvpCommand = Cmd(_ => { editing = null; EditName = EditMap = EditNotes = ""; EditMin = EditMax = 60; EditorExpanded = true; NotifyEditor(); });
        EditMvpCommand = Cmd(p => { var r = Row(p); if (r == null) return; editing = r.Model; EditName = editing.Name; EditMap = editing.Map; EditMin = editing.MinMinutes; EditMax = editing.MaxMinutes; EditNotes = editing.Notes; EditorExpanded = true; NotifyEditor(); });
        DeleteMvpCommand = Cmd(p => { var r = Row(p); if (r == null) return; if (MessageBox.Show($"Excluir {r.Name}? O histórico será preservado.", "Excluir MVP", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; Engine.Mvps.Remove(r.Model); if (editing == r.Model) editing = null; Commit(); });
        KillCommand = Cmd(p => { var r = Row(p); if (r == null) return; Engine.Kill(r.Model, DateTimeOffset.UtcNow); Commit(); AutoCompact(); });
        CorrectDeathCommand = Cmd(p => { var r = Row(p); if (r != null && TryLocal(ManualDeath, out var t)) { Engine.Kill(r.Model, t); Commit(); AutoCompact(); } });
        SaveMvpCommand = Cmd(_ => SaveMvp());
        SaveSettingsCommand = Cmd(_ => { PersistenceService.Validate(Settings); Save(); SettingsChanged?.Invoke(); Refresh(); Tell("Preferências salvas. Durações da mina valem para o próximo ciclo ou correção."); });
        CompactCommand = Cmd(_ => OpenCompact?.Invoke()); PanelCommand = Cmd(_ => OpenPanel?.Invoke());
        PinCommand = Cmd(_ => { Settings.Topmost = !Settings.Topmost; Save(); SettingsChanged?.Invoke(); Changed(nameof(PinLabel)); });
        ExpandCommand = Cmd(_ => { expanded = !expanded; RefreshLists(); Changed(nameof(ExpandLabel)); });
        ExportCommand = Cmd(_ => Export()); ImportCommand = Cmd(_ => Import());
        NavigateCommand = Cmd(p => Page = int.Parse(p?.ToString() ?? "0"));
        foreach (var warning in store.Warnings) Engine.Record("Recuperação", warning, DateTimeOffset.UtcNow);
        Refresh();
    }
    private ICommand Cmd(Action<object?> action) => new RelayCommand(p => { try { action(p); } catch (Exception ex) { Tell("Não foi possível concluir: " + ex.Message); MessageBox.Show(Feedback, "Farmland Companion", MessageBoxButton.OK, MessageBoxImage.Warning); } });
    private MvpRow? Row(object? p) => p as MvpRow ?? SelectedMvp;
    public void Tell(string text) { Feedback = text; Changed(nameof(Feedback)); }
    private bool TryLocal(string text, out DateTimeOffset result)
    {
        if (DateTime.TryParseExact(text, "dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var d) && !TimeZoneInfo.Local.IsInvalidTime(d))
        { result = new DateTimeOffset(d).ToUniversalTime(); if (result <= DateTimeOffset.UtcNow) return true; }
        result = default; Tell("Informe data local válida, não futura: dd/MM/aaaa HH:mm:ss."); return false;
    }
    private void NotifyEditor() { foreach (var n in new[] { nameof(EditName), nameof(EditMap), nameof(EditMin), nameof(EditMax), nameof(EditNotes) }) Changed(n); }
    private void SaveMvp()
    {
        var candidate = new Mvp { Name = EditName.Trim(), Map = EditMap.Trim(), MinMinutes = EditMin, MaxMinutes = EditMax, Notes = EditNotes.Trim() };
        if (!PersistenceService.Valid(candidate)) { Tell("Preencha nome e mapa. Respawn: 0 < mínimo ≤ máximo ≤ 10080 minutos."); return; }
        var m = editing ?? new Mvp();
        m.Name = candidate.Name; m.Map = candidate.Map; m.MinMinutes = candidate.MinMinutes; m.MaxMinutes = candidate.MaxMinutes; m.Notes = candidate.Notes;m.HasRespawn=true;
        m.Origin = "Personalizado pelo usuário";
        // Recalculate corrected configurations, keeping already emitted notification keys.
        if (editing == null) Engine.Mvps.Add(m);
        Catalog.UpdateManualConfiguration(m);
        editing = m; Commit(); Tell("MVP salvo. Os tempos são definidos por você conforme o servidor.");
    }
    public void Tick()
    {
        Events.Tick();
        Spotlight.Tick();
        if (Engine.Evaluate(DateTimeOffset.UtcNow)) Save();
        Refresh();
    }
    private void AutoCompact() { if (Settings.CompactOnTimer) OpenCompact?.Invoke(); }
    private void Commit() { Engine.Evaluate(DateTimeOffset.UtcNow); Save(); Refresh(); }
    public void RegisterCatalogDeath(CatalogEntry entry, double min, double max, string origin, DateTimeOffset death,bool hasRespawn=true)
    {
        var m = Engine.Mvps.FirstOrDefault(x => x.CatalogKey == entry.Key);
        if (m == null)
        {
            m = new Mvp { Name = entry.Name, Map = string.IsNullOrWhiteSpace(entry.Map) ? "Mapa não informado" : entry.Map, CatalogKey = entry.Key };
            Engine.Mvps.Add(m);
        }
        m.MinMinutes = min; m.MaxMinutes = max; m.Origin = origin;m.HasRespawn=hasRespawn;
        Engine.Kill(m, death); Commit(); AutoCompact();
    }
    public void Save()
    {
        Store.Save("settings.json", Settings); Store.Save("mining.json", Engine.Mining); Store.Save("mvps.json", Engine.Mvps); Store.Save("history.json", Engine.History); Store.Save("window-layout.json", Layout);
    }
    public void SaveLayout() => Store.Save("window-layout.json", Layout);
    public void Refresh()
    {
        foreach (var n in new[] { nameof(MiningStatus), nameof(MiningCountdown), nameof(MiningSummary), nameof(MiningProgress), nameof(Entered), nameof(Exit), nameof(Ready), nameof(Count), nameof(ShowMining), nameof(PinLabel), nameof(MiningAvailable), nameof(MiningColor), nameof(MiningNext) }) Changed(n);
        RefreshLists();
        RefreshCompactTabs();
        var events = UpcomingEventsService.Get(Engine, DateTimeOffset.UtcNow);
        UpcomingEvents.Clear(); foreach (var e in events) UpcomingEvents.Add(e); Changed(nameof(NoEvents));
        if (lastHistoryCount != Engine.History.Count || (History.Count > 0 && !ReferenceEquals(History[0], Engine.History.FirstOrDefault())))
        {
            History.Clear(); foreach (var h in Engine.History) History.Add(h);
            RecentAlerts.Clear(); foreach (var h in Engine.History.Where(h => h.Kind == "Alerta").Take(8)) RecentAlerts.Add(h);
            lastHistoryCount = Engine.History.Count;
        }
    }
    private void RefreshLists()
    {
        var all = Engine.Ordered(DateTimeOffset.UtcNow).ToList();
        Sync(Rows, all.Where(m => m.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && m.Map.Contains(mapFilter, StringComparison.OrdinalIgnoreCase)));
        Sync(NextRows, all.Where(m => m.DeathUtc != null).Take(5));
        Sync(CompactAllRows, all.Where(m => m.DeathUtc != null));
        Catalog?.RefreshManualEntries();Catalog?.RefreshFavoriteTimers();
        Sync(CompactRows, Settings.CompactMode == "Mineração" ? Array.Empty<Mvp>() : all.Where(m => m.DeathUtc != null).Take(expanded ? int.MaxValue : Settings.CompactCount));
        Changed(nameof(CompactEmpty));
        Changed(nameof(NoCompactMvps));
        Changed(nameof(NoActiveMvps));
    }
    private void Sync(ObservableCollection<MvpRow> rows, IEnumerable<Mvp> source)
    {
        var list = source.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (i >= rows.Count || !ReferenceEquals(rows[i].Model, list[i]))
            { var found = rows.FirstOrDefault(r => ReferenceEquals(r.Model, list[i])); if (found != null) rows.Move(rows.IndexOf(found), i); else rows.Insert(i, new MvpRow(list[i], Engine)); }
            rows[i].Refresh();
        }
        while (rows.Count > list.Count) rows.RemoveAt(rows.Count - 1);
    }
    private sealed class ExportData
    {
        public int Version { get; set; } = 1;
        public Settings Settings { get; set; } = new();
        public List<Mvp> Mvps { get; set; } = new();
    }
    private void Export()
    {
        var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "ragnarok-config.json" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(new ExportData { Settings = Settings, Mvps = Engine.Mvps }, new JsonSerializerOptions { WriteIndented = true }));
        Tell("Configurações exportadas.");
    }
    private void Import()
    {
        var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
        if (dlg.ShowDialog() != true) return;
        var data = JsonSerializer.Deserialize<ExportData>(File.ReadAllText(dlg.FileName)) ?? throw new InvalidDataException("JSON vazio.");
        if (data.Version != 1 || data.Mvps == null || data.Settings == null || data.Mvps.Any(m => !PersistenceService.Valid(m)) || data.Mvps.GroupBy(m => m.Id).Any(g => g.Count() > 1)) throw new InvalidDataException("Configuração inválida ou versão não suportada.");
        if (MessageBox.Show($"Substituir preferências e cadastros por {data.Mvps.Count} MVPs? A mina e o histórico serão preservados.", "Importar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        PersistenceService.Validate(data.Settings);
        // Copy into the shared settings instance so both windows retain their binding source.
        foreach (var p in typeof(Settings).GetProperties()) p.SetValue(Settings, p.GetValue(data.Settings));
        Engine.Mvps.Clear(); Engine.Mvps.AddRange(data.Mvps); editing = null; SelectedMvp = null;
        Commit(); SettingsChanged?.Invoke(); Tell("Configurações importadas.");
    }
}
