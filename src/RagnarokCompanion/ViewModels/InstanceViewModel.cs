using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class InstanceRow:Observable
{
    readonly InstanceViewModel owner;ImageSource? sprite;bool requested;Task spriteTask=Task.CompletedTask;
    public InstanceDefinition Definition {get;}
    public InstanceRow(InstanceDefinition definition,InstanceViewModel owner)
    {
        Definition=definition;this.owner=owner;
        NowCommand=new RelayCommand(_=>owner.Finish(this,owner.Now));
        TimeCommand=new RelayCommand(_=>{ManualTime=TimeZoneInfo.ConvertTime(owner.Now,TimeZoneInfo.Local).ToString("HH:mm");Yesterday=false;ManualOpen=!ManualOpen;Changed(nameof(ManualTime));Changed(nameof(Yesterday));});
        SaveTimeCommand=new RelayCommand(_=>{if(MvpDeathTimeService.TryResolve(ManualTime,Yesterday,owner.Now,TimeZoneInfo.Local,out var time,out var error)){owner.Finish(this,time);ManualOpen=false;}else owner.Tell(error.Replace("morte","término"));});
        ClearCommand=new RelayCommand(_=>owner.Clear(Definition.Id));
    }
    public string Name=>Definition.Name;
    public string Cooldown=>$"Cooldown: {Definition.CooldownHours}h após o término";
    public string Status=>owner.Service.Status(Definition,owner.Now);
    public TimeSpan Remaining=>owner.Service.Remaining(Definition,owner.Now);
    public string Countdown {get{var t=TimeSpan.FromSeconds(Math.Ceiling(Remaining.TotalSeconds));return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}}
    public string Available=>owner.Service.AvailableAt(Definition) is {} time?"Disponível em "+time.ToLocalTime().ToString("dd/MM HH:mm"):"Registre quando terminar a instância.";
    public string Color=>Status=="Em espera"?"#FFB767":"#6CD9A5";
    public string NpcHint=>Definition.NpcSpriteId>0?Definition.NpcName+" • "+(Definition.FarmlandNpc?"ID confirmado pelo usuário para o Farmland.":"sprite da referência pública Renewal; a aparência no Farmland pode diferir."):"Sprite do NPC ainda não confirmada. Fallback temporário.";
    public string ManualTime {get;set;}="";
    public bool Yesterday {get;set;}
    bool manualOpen;
    public bool ManualOpen {get=>manualOpen;set{manualOpen=value;Changed();}}
    public ICommand NowCommand {get;}public ICommand TimeCommand {get;}public ICommand SaveTimeCommand {get;}public ICommand ClearCommand {get;}
    public ImageSource? Sprite {get{_=EnsureSpriteAsync();return sprite;}}
    public Task EnsureSpriteAsync(){if(!requested){requested=true;spriteTask=LoadSprite();}return spriteTask;}
    async Task LoadSprite(){sprite=await MvpSpriteCache.GetAsync(Definition.NpcSpriteId,owner.Online);Changed(nameof(Sprite));Changed(nameof(HasSprite));}
    public bool HasSprite=>sprite!=null;
    public void Refresh(){foreach(var name in new[]{nameof(Status),nameof(Countdown),nameof(Available),nameof(Color)})Changed(name);}
}
public sealed class InstanceViewModel:Observable
{
    readonly MainViewModel owner;readonly Func<DateTimeOffset> clock;
    public InstanceTimerService Service {get;}
    public ObservableCollection<InstanceRow> Rows {get;}=new();
    public ObservableCollection<InstanceRow> CompactRows {get;}=new();
    public DateTimeOffset Now=>clock();public bool Online=>owner.Store.IsDefaultLocation;
    public bool Sounds {get=>owner.Settings.InstanceSounds;set{owner.Settings.InstanceSounds=value;owner.Save();Changed();}}
    public bool Alerts {get=>owner.Settings.InstanceAlerts;set{owner.Settings.InstanceAlerts=value;owner.Save();Changed();}}
    public string Message {get;private set;}="Os cooldowns começam após o término. Horários informados usam o fuso local do computador.";
    public ICommand ClearAllCommand {get;}
    public event Action<HistoryEntry,bool>? Alert;
    public InstanceViewModel(MainViewModel owner,Func<DateTimeOffset>? clock=null)
    {
        this.owner=owner;this.clock=clock??(()=>DateTimeOffset.UtcNow);Service=new(owner.Store);
        foreach(var definition in InstanceCatalog.Entries)Rows.Add(new(definition,this));
        Service.Ready+=a=>{var entry=owner.Engine.Record("Instâncias",a.Name+" liberada!",a.Utc);owner.Save();if(Alerts)Alert?.Invoke(entry,Sounds);};
        ClearAllCommand=new RelayCommand(_=>Clear(null));Refresh();
    }
    public void Tell(string text){Message=text;Changed(nameof(Message));}
    public void Finish(InstanceRow row,DateTimeOffset time)
    {
        try{Service.Finish(row.Definition.Id,time,Now);owner.Engine.Record("Instâncias",row.Name+" • término registrado",time);owner.Save();Refresh();Tell(row.Name+": "+row.Available);}
        catch(Exception e){Tell("Não foi possível registrar o término: "+e.Message);}
    }
    public void Clear(string? id)
    {
        try{Service.Clear(id);Refresh();Tell(id==null?"Timers das instâncias limpos. Os demais módulos foram preservados.":"Timer da instância limpo.");}catch(Exception e){Tell("Não foi possível limpar: "+e.Message);}
    }
    public void Tick(){Service.Evaluate(Now);Refresh();}
    public void Refresh()
    {
        foreach(var row in Rows)row.Refresh();
        var ordered=Rows.OrderBy(r=>Service.AvailableAt(r.Definition)==null?1:0).ThenBy(r=>r.Remaining).ThenBy(r=>r.Name).ToArray();
        for(int i=0;i<ordered.Length;i++){var row=ordered[i];if(!CompactRows.Contains(row))CompactRows.Insert(i,row);else if(CompactRows.IndexOf(row)!=i)CompactRows.Move(CompactRows.IndexOf(row),i);}
    }
}
