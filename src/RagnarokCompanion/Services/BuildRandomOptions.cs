using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed class BuildRandomOption
{
    public string Id {get;set;}="";public string Name {get;set;}="";public string Range {get;set;}="";public int Minimum {get;set;}public int Maximum {get;set;}public bool Percent {get;set;}public string Category {get;set;}="";public string Profile {get;set;}="Padrão";public bool Negative {get;set;}
    public string Label=>Name; public static string NameOnly(string text)=>System.Text.RegularExpressions.Regex.Replace(text,@"\s+[+-]?\d+(?:[.,]\d+)?\s*%?\s*$", "").Trim();
    public string Format(int value)=>Name+" "+(value>=0?"+":"")+value+(Percent?"%":"");
}
public sealed class BuildOptionRule {public int ItemId {get;set;}public List<string> AllowedSlots {get;set;}=new();public List<string> AllowedOptionIds {get;set;}=new();}
public sealed class BuildRandomOptions
{
    public sealed class Document {public string Version {get;set;}="";public string Source {get;set;}="";public List<BuildRandomOption> Options {get;set;}=new();}
    readonly Document document;readonly Dictionary<int,BuildOptionRule> overrides=new();
    public string Source=>document.Source;
    public BuildRandomOptions(string? root=null)
    {
        using var stream=typeof(BuildRandomOptions).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.build-random-options.json")!;document=JsonSerializer.Deserialize<Document>(stream)??new();
        if(root==null)return;string path=Path.Combine(root,"build-random-option-rules.json");
        if(!File.Exists(path))return;try{if(new FileInfo(path).Length>1_000_000)throw new InvalidDataException("Regras de opções acima do limite.");foreach(var rule in JsonSerializer.Deserialize<List<BuildOptionRule>>(File.ReadAllText(path))??new())if(rule.ItemId>0&&rule.AllowedSlots!=null&&rule.AllowedOptionIds!=null)overrides[rule.ItemId]=rule;}catch(Exception e){new BuildLog(root).Write("random-option-rules",null,e);}
    }
    public static string Category(BuildSlot slot)=>slot.Group=="Shadow"?"Shadows":slot.Id is "weapon" or "accessory-left" or "accessory-right"?"Armas e acessórios":"Outros equipamentos";
    public static string[] Profiles(BuildSlot slot)=>slot.Id=="weapon"?new[]{"Padrão","Forjado • rank 1–5","Forjado • rank 6–10","Forjado • sem ranking"}:new[]{"Padrão"};
    public List<BuildRandomOption> For(BuildSlot slot,int? itemId,BuildItemMetadata? metadata,string profile)
    {
        if(slot.Group=="Pet"||itemId==null||!Profiles(slot).Contains(profile))return new();
        IEnumerable<BuildRandomOption> rows=document.Options.Where(o=>!o.Negative&&o.Category==Category(slot)&&o.Profile==profile);
        if(overrides.TryGetValue(itemId.Value,out var rule)){if(!rule.AllowedSlots.Contains(slot.Id))return new();rows=rows.Where(o=>rule.AllowedOptionIds.Contains(o.Id));}
        else if(metadata==null||!BuildEquipmentCatalog.At(metadata,slot.Location))return new();
        return rows.ToList();
    }
}
