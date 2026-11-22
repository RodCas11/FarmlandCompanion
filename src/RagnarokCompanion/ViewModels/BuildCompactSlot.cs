using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.ViewModels;
public sealed class SlotComponentLine
{
 readonly BuildViewModel owner;readonly bool enchant;readonly int index;
 public string Name {get;}public string Kind=>enchant?"Encantamento":"Carta";
 public SlotComponentLine(BuildViewModel owner,int id,bool enchant,int index){this.owner=owner;this.enchant=enchant;this.index=index;Name=owner.FindItem(id)?.Name??"#"+id;}
 public ICommand RemoveCommand=>new RelayCommand(_=>{var row=owner.SelectedSlot;if(row==null)return;var list=enchant?row.Model.Enchants:row.Model.Cards;if(index>=list.Count)return;list.RemoveAt(index);row.ComponentsChanged();owner.Edited();});
}
public sealed class SlotOptionLine:Observable
{
 readonly BuildViewModel owner;readonly int index;public SlotOptionLine(BuildViewModel owner,int index){this.owner=owner;this.index=index;}
 public string Text {get=>owner.SelectedSlot?.Model.RandomOptions.ElementAtOrDefault(index)??"";set{var row=owner.SelectedSlot;if(row==null||index>=row.Model.RandomOptions.Count)return;row.Model.RandomOptions[index]=RagnarokCompanion.Services.BuildRandomOption.NameOnly(value);Changed();owner.CompactOptionEdited();}}
 public ICommand RemoveCommand=>new RelayCommand(_=>{var row=owner.SelectedSlot;if(row==null||index>=row.Model.RandomOptions.Count)return;row.Model.RandomOptions.RemoveAt(index);row.Refresh();owner.Edited();});
}
public sealed partial class BuildViewModel
{
 public ObservableCollection<SlotComponentLine> SlotComponents {get;}=new();public ObservableCollection<SlotOptionLine> SlotOptions {get;}=new();
 public System.Collections.Generic.IEnumerable<SlotComponentLine> CardComponents=>SlotComponents.Where(c=>c.Kind=="Carta");
 public System.Collections.Generic.IEnumerable<SlotComponentLine> EnchantComponents=>SlotComponents.Where(c=>c.Kind=="Encantamento");
 public ObservableCollection<FarmItem> CardResults {get;}=new();public ObservableCollection<FarmItem> EnchantResults {get;}=new();string cardQuery="",enchantQuery="";
 public string CardQuery {get=>cardQuery;set{cardQuery=value??"";Changed();FindComponents(cardQuery,false,CardResults);}}
 public string EnchantQuery {get=>enchantQuery;set{enchantQuery=value??"";Changed();FindComponents(enchantQuery,true,EnchantResults);}}
 public static bool EnchantLevelAllowed(string name){var match=System.Text.RegularExpressions.Regex.Match(name,@"(?:\+|\s)(\d+)\s*$");return !match.Success||int.TryParse(match.Groups[1].Value,out int level)&&level<=6;}
 void FindComponents(string query,bool enchant,ObservableCollection<FarmItem> results){results.Clear();if(string.IsNullOrWhiteSpace(query))return;foreach(var item in Items.Search(query,i=>Equipment.Find(i.Id) is {Type:"Card"} m&&(m.SubType=="Enchant")==enchant&&(!enchant||EnchantLevelAllowed(i.Name))))results.Add(item);}
 public void AddSlotComponent(FarmItem item,bool enchant){if(SelectedSlot==null||Equipment.Find(item.Id) is not {Type:"Card"} meta||(meta.SubType=="Enchant")!=enchant)return;if(enchant&&!EnchantLevelAllowed(item.Name)){Tell("Encantamentos permitidos somente até +6.");return;}var list=enchant?SelectedSlot.Model.Enchants:SelectedSlot.Model.Cards;if(list.Count>=4){Tell("Limite de quatro componentes por tipo.");return;}list.Add(item.Id);SelectedSlot.ComponentsChanged();Edited();if(enchant)EnchantQuery="";else CardQuery="";}
 bool updatingCompact;
 void RefreshCompactSlot(){if(!transientEditor||updatingCompact)return;SlotComponents.Clear();SlotOptions.Clear();var row=SelectedSlot;if(row==null)return;for(int i=0;i<row.Model.Cards.Count;i++)SlotComponents.Add(new(this,row.Model.Cards[i],false,i));for(int i=0;i<row.Model.Enchants.Count;i++)SlotComponents.Add(new(this,row.Model.Enchants[i],true,i));for(int i=0;i<row.Model.RandomOptions.Count;i++)SlotOptions.Add(new(this,i));Changed(nameof(CardComponents));Changed(nameof(EnchantComponents));}
 public void CompactOptionEdited(){updatingCompact=true;try{SelectedSlot?.Refresh();Edited();}finally{updatingCompact=false;}}
}
