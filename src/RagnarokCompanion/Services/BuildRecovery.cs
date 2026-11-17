using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class BuildRecovery
{
    public static List<string> Normalize(PlayerBuild build,string? storedId=null)
    {
        var warnings=new List<string>();
        if(!Guid.TryParseExact(build.Id,"N",out _)){build.Id=Guid.TryParseExact(storedId,"N",out _)?storedId!:Guid.NewGuid().ToString("N");warnings.Add("Identificador recuperado.");}
        if(string.IsNullOrWhiteSpace(build.Title)){build.Title="Build recuperada";warnings.Add("Nome ausente.");}
        if(!BuildCatalog.Classes.Any(c=>c.Id==build.ClassId)){build.ClassId="lord";warnings.Add("Classe ausente ou desconhecida; confira a seleção.");}
        if(!BuildCatalog.Stages.Contains(build.Stage)){build.Stage="Early";warnings.Add("Estágio ausente ou desconhecido.");}
        if(!BuildCatalog.Purposes.Contains(build.Purpose)){build.Purpose="PvM";warnings.Add("Finalidade ausente ou desconhecida.");}
        build.Notes??="";
        if(build.Stats==null){build.Stats=new();warnings.Add("Atributos ausentes.");}
        foreach(var stat in new[]{"FOR","AGI","VIT","INT","DES","SOR"})if(!build.Stats.TryGetValue(stat,out int value)||value is <1 or >999){build.Stats[stat]=1;warnings.Add("Confira "+stat+".");}
        if(build.Equipment==null){build.Equipment=new();warnings.Add("Equipamentos ausentes.");}
        var repaired=new List<BuildEquipment>();
        foreach(var slot in BuildCatalog.Slots)
        {
            var matches=build.Equipment.Where(e=>e!=null&&e.Slot==slot.Id).ToArray();var item=matches.FirstOrDefault(e=>e.ItemId>0)??matches.FirstOrDefault()??new(){Slot=slot.Id};
            if(matches.Length>1)warnings.Add(slot.Name+": registros duplicados; confira o item preservado.");
            if(item.ItemId<=0)item.ItemId=null;
            if(item.Cards==null||item.Enchants==null||item.RandomOptions==null)warnings.Add(slot.Name+": componentes ausentes foram recuperados.");
            item.Cards=(item.Cards??new()).Where(id=>id>0).ToList();item.Enchants=(item.Enchants??new()).Where(id=>id>0).ToList();item.RandomOptions=(item.RandomOptions??new()).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(RagnarokCompanion.Services.BuildRandomOption.NameOnly).ToList();
            item.RandomOptionProfile=string.IsNullOrWhiteSpace(item.RandomOptionProfile)?"Padrão":item.RandomOptionProfile;
            if(item.Refine is <0 or >20){item.Refine=0;warnings.Add(slot.Name+": refino inválido; informe novamente.");}
            if(!BuildCatalog.Grades.Contains(item.Grade)){item.Grade="Sem";warnings.Add(slot.Name+": grade ausente ou inválida.");}
            repaired.Add(item);
        }
        if(build.Equipment.Any(e=>e==null||!BuildCatalog.Slots.Any(s=>s.Id==e.Slot)))warnings.Add("Slots desconhecidos ou vazios não foram exibidos; o original foi preservado para recuperação.");
        build.Equipment=repaired;
        if(build.UpdatedUtc==default)build.UpdatedUtc=DateTimeOffset.UtcNow;
        return warnings;
    }
}
public sealed class BuildLog
{
    readonly string root;public string Path=>System.IO.Path.Combine(root,"logs","builds.log");
    public BuildLog(string root)=>this.root=root;
    public void Write(string operation,string? id,Exception? error=null,string? details=null)
    {
        try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);if(File.Exists(Path)&&new FileInfo(Path).Length>2_000_000)File.Move(Path,Path+".previous",true);File.AppendAllText(Path,$"{DateTimeOffset.UtcNow:O} | {operation} | build={id ?? "unknown"} | {details}\n{error}\n",Encoding.UTF8);}catch{}
    }
    public void Preserve(string id,string payload)
    {
        try{string directory=System.IO.Path.Combine(root,"build-recovery");Directory.CreateDirectory(directory);string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..20];string file=System.IO.Path.Combine(directory,hash+".json");if(!File.Exists(file))File.WriteAllText(file,payload,Encoding.UTF8);}catch(Exception e){Write("backup-recovery",id,e);}
    }
}
