using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed class MvpCardRow:Observable {
 readonly MainViewModel owner;readonly CatalogViewModel catalog;readonly FavoriteTimerRow timer;bool requested;ImageSource? sprite;
 public CatalogEntry Entry {get;}
 public MvpCardRow(CatalogEntry entry,MainViewModel owner,CatalogViewModel catalog){Entry=entry;this.owner=owner;this.catalog=catalog;timer=new(entry,owner);NowCommand=new RelayCommand(_=>catalog.QuickDeathCommand.Execute(entry));TimeCommand=new RelayCommand(_=>catalog.InformCardTime(entry));FavoriteCommand=new RelayCommand(_=>catalog.ToggleFavoriteCommand.Execute(entry));}
 public string Name=>Entry.Name;public string Map=>Entry.DisplayMap;public string Id=>(Entry.MonsterId>0?"#"+Entry.MonsterId+" · ":"")+(Entry.CustomMin!=null?"Ajuste pessoal":Entry.Origin.StartsWith("Farmland",StringComparison.Ordinal)?"Farmland":"Referência");public string FavoriteMark=>Entry.FavoriteMark;
 public string Status=>timer.Status;public string Countdown=>owner.Engine.Mvps.FirstOrDefault(m=>m.CatalogKey==Entry.Key&&m.DeathUtc!=null) is {} m?new MvpRow(m,owner.Engine).Countdown:"Sem timer";
 public string Window=>owner.Engine.Mvps.FirstOrDefault(m=>m.CatalogKey==Entry.Key&&m.DeathUtc!=null) is {} m?new MvpRow(m,owner.Engine).Window:"";
 public string Color=>timer.Color;public ICommand NowCommand {get;}public ICommand TimeCommand {get;}public ICommand FavoriteCommand {get;}
 public ImageSource? Sprite {get{if(!requested){requested=true;_=LoadSprite();}return sprite;}}
 public bool HasSprite=>sprite!=null;
 async Task LoadSprite(){sprite=await MvpSpriteCache.GetAsync(Entry.MonsterId,owner.Store.IsDefaultLocation);Changed(nameof(Sprite));Changed(nameof(HasSprite));}
 public void Refresh(){foreach(var name in new[]{nameof(Name),nameof(Map),nameof(FavoriteMark),nameof(Status),nameof(Countdown),nameof(Window),nameof(Color)})Changed(name);}
}
public sealed partial class CatalogViewModel {
 readonly Dictionary<string,MvpCardRow> cardCache=new();
 public ObservableCollection<MvpCardRow> Cards {get;}=new();
 bool manualOpen;
 public bool ManualOpen {get=>manualOpen;set{manualOpen=value;Changed();}}
 public ICommand CloseManualCommand=>new RelayCommand(_=>ManualOpen=false);
 public void InformCardTime(CatalogEntry entry){SelectedEntry=entry;DeathTime=simpleClock().ToLocalTime().ToString("HH:mm");PreviousDay=false;Changed(nameof(DeathTime));Changed(nameof(PreviousDay));ManualOpen=true;}
 void RefreshCards(){Cards.Clear();foreach(var entry in Entries){if(!cardCache.TryGetValue(entry.Key,out var card)||!ReferenceEquals(card.Entry,entry))cardCache[entry.Key]=card=new(entry,owner,this);card.Refresh();Cards.Add(card);}}
}
