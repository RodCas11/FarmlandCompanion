using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed class MvpRespawnRecord
{
    public int MonsterId {get;set;}
    public string MonsterName {get;set;}="";
    public string MapCode {get;set;}="";
    public string SpawnIdentifier {get;set;}="";
    public string ScheduleType {get;set;}="Interval";
    public double RespawnBaseSeconds {get;set;}
    public double RespawnVarianceSeconds {get;set;}
    public double RespawnMinSeconds {get;set;}
    public double RespawnMaxSeconds {get;set;}
    public string Source {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string SourceUrl {get;set;}="";
    public bool IsFarmlandConfirmed {get;set;}
    public DateTimeOffset LastVerifiedAt {get;set;}
}
public sealed class RespawnCatalogDocument
{
    public int Version {get;set;}=1;
    public List<MvpRespawnRecord> Records {get;set;}=new();
}
public sealed class MvpRespawnCatalog
{
    readonly PersistenceService? store;
    public List<MvpRespawnRecord> Records {get;}=new();
    public MvpRespawnCatalog(PersistenceService? store=null,IEnumerable<MvpRespawnRecord>? records=null){
        this.store=store;
        if(records!=null)Records.AddRange(records);
        else{using var stream=typeof(MvpRespawnCatalog).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.mvp-respawns.json")!;Records.AddRange(JsonSerializer.Deserialize<RespawnCatalogDocument>(stream)!.Records);}
        if(store!=null){var custom=store.Load<RespawnCatalogDocument>("mvp-respawns-custom.json");if(Valid(custom))Records.InsertRange(0,custom.Records);}
    }
    public static bool Valid(RespawnCatalogDocument? data)=>data is {Version:1,Records:not null}&&data.Records.Count<=5000&&data.Records.All(r=>r!=null&&r.ScheduleType=="Interval"&&r.MonsterId>0&&!string.IsNullOrWhiteSpace(r.MonsterName)&&!string.IsNullOrWhiteSpace(r.MapCode)&&!string.IsNullOrWhiteSpace(r.Source)&&!string.IsNullOrWhiteSpace(r.SourceVersion)&&r.LastVerifiedAt!=default&&MvpRespawnService.Valid(r.RespawnMinSeconds/60,r.RespawnMaxSeconds/60)&&double.IsFinite(r.RespawnBaseSeconds)&&double.IsFinite(r.RespawnVarianceSeconds)&&r.RespawnBaseSeconds>0&&r.RespawnVarianceSeconds>=0&&Math.Abs(r.RespawnMinSeconds-r.RespawnBaseSeconds)<0.001&&Math.Abs(r.RespawnMaxSeconds-r.RespawnBaseSeconds-r.RespawnVarianceSeconds)<0.001);
    public void Import(string path){
        if(new FileInfo(path).Length>5_000_000)throw new InvalidDataException("Catálogo maior que o limite permitido.");
        var data=JsonSerializer.Deserialize<RespawnCatalogDocument>(File.ReadAllText(path));
        if(!Valid(data)||data!.Records.Count==0)throw new InvalidDataException("Catálogo inválido ou sem fontes verificáveis.");
        store?.Save("mvp-respawns-custom.json",data);
        Records.RemoveAll(r=>data.Records.Any(n=>n.MonsterId==r.MonsterId&&n.MapCode==r.MapCode&&n.SpawnIdentifier==r.SpawnIdentifier));Records.InsertRange(0,data.Records);
    }
    public MvpRespawnRecord? Find(CatalogEntry entry,bool farmlandOnly=false){
        var rows=Records.Where(r=>r.MonsterId==entry.MonsterId&&(!farmlandOnly||r.IsFarmlandConfirmed));
        if(!string.IsNullOrEmpty(entry.Map))rows=rows.Where(r=>r.MapCode==entry.Map);
        if(!string.IsNullOrEmpty(entry.SpawnIdentifier))rows=rows.Where(r=>r.SpawnIdentifier==entry.SpawnIdentifier||string.IsNullOrEmpty(r.SpawnIdentifier));
        var choices=rows.OrderByDescending(r=>r.IsFarmlandConfirmed).ToList();
        if(choices.Count==0)return null;
        if(choices.Any(r=>r.IsFarmlandConfirmed))choices=choices.Where(r=>r.IsFarmlandConfirmed).ToList();
        // Never merge maps or different spawn intervals into a fabricated window.
        if(choices.Select(r=>(r.MapCode,r.RespawnMinSeconds,r.RespawnMaxSeconds)).Distinct().Count()!=1)return null;
        return choices[0];
    }
}
