using System;
using System.Collections.Generic;
namespace RagnarokCompanion.Models;
public sealed record BuildClass(string Id,string Name,string Job,string Family,string Sprite,bool Upper=true,string Gender="Both") {public override string ToString()=>Name;}
public sealed record BuildSlot(string Id,string Name,string Group,string Location);
public sealed class BuildEquipment
{
    public string Slot {get;set;}="";public int? ItemId {get;set;}public int Refine {get;set;}public string Grade {get;set;}="Sem";
    public List<int> Cards {get;set;}=new();public List<int> Enchants {get;set;}=new();public List<string> RandomOptions {get;set;}=new();
    public string RandomOptionProfile {get;set;}="Padrão";
}
public sealed class PlayerBuild
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");public string Title {get;set;}="Minha build";public string ClassId {get;set;}="lord";public string Stage {get;set;}="Early";public string Purpose {get;set;}="PvM";
    public Dictionary<string,int> Stats {get;set;}=new(){{"FOR",1},{"AGI",1},{"VIT",1},{"INT",1},{"DES",1},{"SOR",1}};
    public List<BuildEquipment> Equipment {get;set;}=new();public string Notes {get;set;}="";public DateTimeOffset UpdatedUtc {get;set;}=DateTimeOffset.UtcNow;
}
public sealed class BuildCriteria
{
    public string Version {get;set;}="farmland-2026-11-03";public int NormalEndRefine {get;set;}=9;public string NormalEndGrade {get;set;}="B";public int ShadowEndRefine {get;set;}=10;
    public int MidCostumeOptions {get;set;}=1;public int EndCostumeOptions {get;set;}=3;public int EndEquipmentOptions {get;set;}=1;
    public Dictionary<string,int> RequiredCards {get;set;}=new();public Dictionary<string,int> RequiredEnchants {get;set;}=new();
}
public sealed class BuildItemMetadata
{
    public int Id {get;set;}public string Type {get;set;}="";public string SubType {get;set;}="";public int Slots {get;set;}public bool Refineable {get;set;}
    public Dictionary<string,bool> Jobs {get;set;}=new();public Dictionary<string,bool> Classes {get;set;}=new();public Dictionary<string,bool> Locations {get;set;}=new();public string Gender {get;set;}="Both";
}
public sealed record CommunityBuild(string Id,PlayerBuild Build,string Status,string AuthorName="",string OwnerUid="",int StarCount=0,bool Starred=false,long Version=0,string UpdatedAt="",bool HasContent=true,string AdminNotes="");
public sealed record BuildCommunitySnapshot(List<CommunityBuild> Builds,List<string> ApprovedCombinations,bool RewardUnlocked,string Revision);
