using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class EventAdvanceOption : Observable
{
    readonly EventViewModel owner;
    public int Minutes { get; }
    public string Label => Minutes + " min";
    public EventAdvanceOption(EventViewModel owner, int minutes) { this.owner=owner; Minutes=minutes; }
    public bool Selected { get => owner.Repository.Settings.AdvanceMinutes.Contains(Minutes); set { var list=owner.Repository.Settings.AdvanceMinutes; if(value&&!list.Contains(Minutes))list.Add(Minutes);if(!value)list.Remove(Minutes);owner.SavePreferences();Changed(); } }
}
public sealed class EventViewModel : Observable
{
    public EventRepository Repository { get; }
    public EventScheduleService Schedule { get; }
    readonly Func<DateTimeOffset> clock;
    public ObservableCollection<EventOccurrence> Today { get; } = new();
    public ObservableCollection<ScheduledEvent> Entries { get; } = new();
    public ObservableCollection<EventAdvanceOption> AdvanceOptions { get; } = new();
    public EventOccurrence? Next { get; private set; }
    public EventOccurrence? Following { get; private set; }
    public bool NoToday => Today.Count == 0;
    public string NextName => Next?.Title ?? "Nenhum evento programado";
    public string NextTiming => Next?.Timing ?? "Ative ou cadastre um evento";
    public string Countdown => Next?.Countdown ?? "—";
    public string FollowingSummary => Following == null ? "" : "Depois: " + Following.Title + " • " + Following.Time;
    public string AlertStatus => !Enabled ? "Alertas desativados" : Next == null ? "Sem eventos futuros" : !Next.Event.AlertsEnabled ? "Alertas desativados para este evento" : EventRepository.Advances(Next.Event.UseGlobalAdvances ? Repository.Settings.AdvanceMinutes : Next.Event.AdvanceMinutes).Count == 0 ? "Nenhuma antecedência selecionada" : "Alertas ativados • enquanto o aplicativo estiver aberto";
    public string ZoneLabel => "Horários no fuso " + Repository.Settings.TimeZoneId;
    public string ToggleLabel => Enabled ? "Desativar alertas" : "Ativar alertas";
    public bool Enabled { get => Repository.Settings.Enabled; set { Repository.Settings.Enabled=value;SavePreferences();Changed(); } }
    public bool Sounds { get => Repository.Settings.Sounds; set { Repository.Settings.Sounds=value;SavePreferences();Changed(); } }
    public bool ShowCompact { get => Repository.Settings.ShowCompact; set { Repository.Settings.ShowCompact=value;SavePreferences();Changed(); } }
    public bool DuduAlerts { get => Repository.Events.First(e=>e.OfficialId=="dudu").AlertsEnabled; set { Repository.Events.First(e=>e.OfficialId=="dudu").AlertsEnabled=value;Repository.SaveEvents();Changed();Refresh(); } }
    public bool KvmAlerts { get => Repository.Events.First(e=>e.OfficialId=="kvm").AlertsEnabled; set { Repository.Events.First(e=>e.OfficialId=="kvm").AlertsEnabled=value;Repository.SaveEvents();Changed();Refresh(); } }
    public string TimeZoneInput { get; set; }
    public string Message { get; private set; } = "Horários fornecidos pela administração. Não confirmam que o evento está ativo no servidor.";
    public string[] Icons { get; } = { "🔔", "👻", "⚔", "🎉", "★", "⛏" };
    ScheduledEvent? selected;
    public ScheduledEvent? SelectedEvent { get => selected; set { selected=value;LoadEditor();Changed(); } }
    public string EditName { get; set; } = "";
    public string EditDescription { get; set; } = "";
    public string EditTimes { get; set; } = "";
    public string EditIcon { get; set; } = "🔔";
    public string EditAdvances { get; set; } = "5";
    public bool EditEnabled { get; set; } = true;
    public bool EditAlerts { get; set; } = true;
    public bool EditOpening { get; set; }
    public bool EditGlobalAdvances { get; set; } = true;
    bool editorExpanded;
    public bool EditorExpanded { get=>editorExpanded;set{editorExpanded=value;Changed();} }
    public ICommand ToggleCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveEventCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveZoneCommand { get; }
    public EventViewModel(EventRepository repository, Func<DateTimeOffset>? clock=null)
    {
        Repository=repository;Schedule=new(repository);this.clock=clock??(()=>DateTimeOffset.UtcNow);TimeZoneInput=repository.Settings.TimeZoneId;
        foreach(int m in EventRepository.AllowedAdvances)AdvanceOptions.Add(new(this,m));
        ReloadEntries();
        ToggleCommand=Command(()=>Enabled=!Enabled);
        NewCommand=Command(()=>{SelectedEvent=null;EditorExpanded=true;});
        EditCommand=Command(()=>{if(selected!=null)EditorExpanded=true;});
        SaveEventCommand=Command(SaveEditor);
        DeleteCommand=Command(()=>{if(selected==null)return;if(!string.IsNullOrEmpty(selected.OfficialId)){Tell("Eventos oficiais podem ser desativados, mas não excluídos.");return;}Repository.Events.Remove(selected);Repository.SaveEvents();SelectedEvent=null;ReloadEntries();Refresh();});
        SaveZoneCommand=Command(()=>{if(!EventScheduleService.TryZone(TimeZoneInput.Trim(),out _)){Tell("Fuso inválido. Exemplo: America/Sao_Paulo ou UTC.");return;}Repository.Settings.TimeZoneId=TimeZoneInput.Trim();SavePreferences();Changed(nameof(ZoneLabel));Tell("Fuso do servidor salvo; horários recalculados.");});
        Refresh();
    }
    ICommand Command(Action action)=>new RelayCommand(_=>{try{action();}catch(Exception){Tell("Não foi possível salvar. Confira o acesso à pasta de dados e tente novamente.");}});
    void Tell(string text){Message=text;Changed(nameof(Message));}
    public void SavePreferences(){Repository.SaveSettings();Refresh();}
    void ReloadEntries(){Entries.Clear();foreach(var e in Repository.Events.OrderBy(e=>string.IsNullOrEmpty(e.OfficialId)).ThenBy(e=>e.Name))Entries.Add(e);}
    void LoadEditor()
    {
        EditName=selected?.Name??"";EditDescription=selected?.Description??"";EditTimes=string.Join(", ",selected?.Times??new());EditIcon=selected?.Icon??"🔔";EditEnabled=selected?.Enabled??true;EditAlerts=selected?.AlertsEnabled??true;EditOpening=selected?.IsOpening??false;EditGlobalAdvances=selected?.UseGlobalAdvances??true;EditAdvances=string.Join(",",selected?.AdvanceMinutes??new(){5});
        foreach(var name in new[]{nameof(EditName),nameof(EditDescription),nameof(EditTimes),nameof(EditIcon),nameof(EditEnabled),nameof(EditAlerts),nameof(EditOpening),nameof(EditGlobalAdvances),nameof(EditAdvances)})Changed(name);
    }
    void SaveEditor()
    {
        var times=EditTimes.Split(new[]{',',';','\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct().OrderBy(t=>t,StringComparer.Ordinal).ToList();
        var parts=EditAdvances.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(parts.Any(s=>!int.TryParse(s,out int n)||!EventRepository.AllowedAdvances.Contains(n))){Tell("Antecedências permitidas: 1, 5, 10, 15 e 30 minutos, separadas por vírgulas.");return;}
        var candidate=new ScheduledEvent{Name=EditName.Trim(),Description=EditDescription.Trim(),Times=times,Icon=EditIcon??"🔔"};
        if(!EventRepository.Valid(candidate)||!Icons.Contains(candidate.Icon)){Tell("Informe nome (até 120 caracteres), descrição (até 4000), ícone e horários válidos em HH:mm, separados por vírgulas (até 48).");return;}
        var item=selected??new ScheduledEvent();
        if(!item.Times.SequenceEqual(times))item.TimesCustomized=true;
        item.Name=candidate.Name;item.Description=candidate.Description;item.Times=times;item.Icon=candidate.Icon;item.Enabled=EditEnabled;item.AlertsEnabled=EditAlerts;item.IsOpening=EditOpening;item.UseGlobalAdvances=EditGlobalAdvances;item.AdvanceMinutes=parts.Select(int.Parse).Distinct().ToList();
        if(selected==null)Repository.Events.Add(item);
        Repository.SaveEvents();ReloadEntries();SelectedEvent=item;Refresh();Changed(nameof(DuduAlerts));Changed(nameof(KvmAlerts));Tell("Evento salvo. Alterações valem para as próximas ocorrências; avisos já registrados não são repetidos.");
    }
    public void Tick(){Schedule.Evaluate(clock());Refresh();}
    public void Refresh()
    {
        var now=clock();var upcoming=Schedule.Upcoming(now);Next=upcoming.FirstOrDefault();Following=upcoming.Skip(1).FirstOrDefault();
        Today.Clear();foreach(var o in Schedule.Occurrences(now,true))Today.Add(o);
        foreach(var name in new[]{nameof(Next),nameof(Following),nameof(NextName),nameof(NextTiming),nameof(Countdown),nameof(FollowingSummary),nameof(AlertStatus),nameof(ToggleLabel),nameof(ZoneLabel),nameof(NoToday)})Changed(name);
    }
}
