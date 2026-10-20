using System.Collections.Generic;
namespace RagnarokCompanion.Models;
public sealed class QuestDefinition {
 public int Id {get;set;} public int ItemId {get;set;} public string Name {get;set;}=""; public string Category {get;set;}=""; public long Zeny {get;set;}
 public string SourceKey {get;set;}="";public string Kind {get;set;}="Raras";public string AegisName {get;set;}="";
 public List<QuestMaterial> Materials {get;set;}=new();
 public string Icon=>ItemId>0?"https://static.divine-pride.net/images/items/item/"+ItemId+".png":"";
 public string Cost=>Zeny.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))+" Zeny";
}
public sealed class QuestMaterial {public int Index {get;set;}public int ItemId {get;set;}public string Name {get;set;}="";public string AegisName {get;set;}="";public long Amount {get;set;}public int Refine {get;set;}public string Label=>Name+(Refine>0?" +"+Refine:"");public string Icon=>ItemId>0?"https://static.divine-pride.net/images/items/item/"+ItemId+".png":"";}
public sealed class QuestProgress {public HashSet<int> Favorites {get;set;}=new();public HashSet<string> Obtained {get;set;}=new();public int SelectedId {get;set;}}
