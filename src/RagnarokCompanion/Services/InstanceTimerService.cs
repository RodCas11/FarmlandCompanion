using System;
using System.Collections.Generic;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;

public static class InstanceCatalog
{
    const string Source="https://github.com/rathena/rathena/blob/master/npc/";
    public static readonly InstanceDefinition[] Entries={
        new("horror-factory","Fábrica do Terror",8,10032,"Catherine Jet Johnson",Source+"re/instances/HorrorToyFactory.txt"),
        new("ghost-palace","Palácio das Mágoas",8,686,"Guarda Real / Sakray",Source+"re/instances/GhostPalace.txt"),
        new("poring-village","Vila Poring",4,612,"4_F_PINKWOMAN","https://nn.ai4rei.net/dev/npclist/?qq=8&s=twro",true),
        new("sarah-memory","Memórias de Sarah",8,945,"Aventureiro Leon",Source+"re/instances/SaraMemory.txt"),
        new("glast-heim","Glast Heim",8,755,"Hugin",Source+"re/instances/OldGlastHeim.txt"),
        new("tower-captain","Capitão da Torre",8,709,"Capitão Janssen",Source+"instances/EndlessTower.txt",false,128),
        new("nydhogg-nest","Ninho do Nydhogg",8,111,"Guardião de Yggdrasil",Source+"instances/NydhoggsNest.txt")
    };
}
// Absolute UTC timestamps; the shared application tick evaluates deadlines after sleep/restart.
public sealed class InstanceTimerService
{
    readonly PersistenceService store;
    public List<InstanceTimer> Timers {get;}
    public event Action<InstanceReadyAlert>? Ready;
    public InstanceTimerService(PersistenceService store)
    {
        this.store=store;
        Timers=store.Load<List<InstanceTimer>>("instance-timers.json").Where(t=>t!=null&&InstanceCatalog.Entries.Any(d=>d.Id==t.Id)&&
            (t.FinishedUtc==null||t.FinishedUtc>=DateTimeOffset.MinValue.AddDays(1)&&t.FinishedUtc<=DateTimeOffset.MaxValue.AddDays(-1)))
            .GroupBy(t=>t.Id).Select(g=>g.First()).ToList();
        foreach(var definition in InstanceCatalog.Entries)if(!Timers.Any(t=>t.Id==definition.Id))Timers.Add(new(){Id=definition.Id});
    }
    public InstanceTimer State(string id)=>Timers.Single(t=>t.Id==id);
    public DateTimeOffset? AvailableAt(InstanceDefinition definition)=>State(definition.Id).FinishedUtc?.AddHours(definition.CooldownHours);
    public TimeSpan Remaining(InstanceDefinition definition,DateTimeOffset now)=>AvailableAt(definition) is {} ready&&ready>now?ready-now:TimeSpan.Zero;
    public string Status(InstanceDefinition definition,DateTimeOffset now)=>AvailableAt(definition) is not {} ready?"Disponível":ready>now?"Em espera":"Liberada";
    public void Save()=>store.Save("instance-timers.json",Timers);
    public void Finish(string id,DateTimeOffset finished,DateTimeOffset now)
    {
        if(finished>now||finished<DateTimeOffset.MinValue.AddDays(1)||finished>DateTimeOffset.MaxValue.AddDays(-1))throw new ArgumentException("Informe um término válido, sem horário futuro.");
        var state=State(id);var previous=state.FinishedUtc;bool notified=state.Notified;state.FinishedUtc=finished.ToUniversalTime();state.Notified=false;
        try{Save();}catch{state.FinishedUtc=previous;state.Notified=notified;throw;}
    }
    public void Clear(string? id=null)
    {
        var affected=Timers.Where(t=>id==null||t.Id==id).Select(t=>(Timer:t,Finished:t.FinishedUtc,Notified:t.Notified)).ToArray();
        foreach(var item in affected){item.Timer.FinishedUtc=null;item.Timer.Notified=false;}
        try{Save();}catch{foreach(var item in affected){item.Timer.FinishedUtc=item.Finished;item.Timer.Notified=item.Notified;}throw;}
    }
    public void Evaluate(DateTimeOffset now)
    {
        var due=InstanceCatalog.Entries.Where(d=>AvailableAt(d) is {} ready&&ready<=now&&!State(d.Id).Notified).ToArray();
        if(due.Length==0)return;foreach(var definition in due)State(definition.Id).Notified=true;
        try{Save();}catch{foreach(var definition in due)State(definition.Id).Notified=false;throw;}
        foreach(var definition in due)Ready?.Invoke(new(definition.Name,now));
    }
}
