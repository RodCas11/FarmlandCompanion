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
 public ObservableCollection<FarmItem> ComponentResults {get;}=new();string componentQuery="",componentKind="Carta";
 public string[] ComponentKinds=>new[]{"Carta","Encantamento"};public string ComponentKind {get=>componentKind;set{componentKind=value;Changed();}}
 public string ComponentQuery {get=>componentQuery;set{componentQuery=value??"";Changed();ComponentResults.Clear();if(componentQuery.Trim().Length>0)foreach(var item in Items.Search(componentQuery,i=>Equipment.Find(i.Id)?.Type=="Card"))ComponentResults.Add(item);}}
 public FarmItem? SelectedComponent {get;set;}
 public ICommand AddSlotComponentCommand=>new RelayCommand(_=>{if(SelectedComponent==null||SelectedSlot==null)return;var list=ComponentKind=="Encantamento"?SelectedSlot.Model.Enchants:SelectedSlot.Model.Cards;if(list.Count>=4){Tell("Limite de quatro componentes por tipo.");return;}list.Add(SelectedComponent.Id);SelectedSlot.ComponentsChanged();Edited();SelectedComponent=null;ComponentQuery="";});
 bool updatingCompact;
 void RefreshCompactSlot(){if(!transientEditor||updatingCompact)return;SlotComponents.Clear();SlotOptions.Clear();var row=SelectedSlot;if(row==null)return;for(int i=0;i<row.Model.Cards.Count;i++)SlotComponents.Add(new(this,row.Model.Cards[i],false,i));for(int i=0;i<row.Model.Enchants.Count;i++)SlotComponents.Add(new(this,row.Model.Enchants[i],true,i));for(int i=0;i<row.Model.RandomOptions.Count;i++)SlotOptions.Add(new(this,i));}
 public void CompactOptionEdited(){updatingCompact=true;try{SelectedSlot?.Refresh();Edited();}finally{updatingCompact=false;}}
}
