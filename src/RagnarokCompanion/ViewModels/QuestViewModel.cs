using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed class QuestRow:Observable,IItemTooltipModel {
 public QuestDefinition Model {get;} readonly QuestViewModel owner;
 public QuestRow(QuestDefinition model,QuestViewModel owner){Model=model;this.owner=owner;}
 public string Name=>Model.Name;public string Category=>Model.Category+owner.RecipeSuffix(Model);
 public string FavoriteMark=>Favorite?"★":"☆";public ICommand FavoriteCommand=>new RelayCommand(_=>Favorite=!Favorite);
 public FarmItem? SelectedItem=>Model.ItemId>0?new(){Id=Model.ItemId,Name=Model.Name}:null;public string ItemName=>Name;public string Icon=>Model.Icon;public ItemDescriptionService Descriptions=>owner.Descriptions;
 public bool Favorite {get=>owner.Progress.Favorites.Contains(Model.Id);set{if(value)owner.Progress.Favorites.Add(Model.Id);else owner.Progress.Favorites.Remove(Model.Id);owner.Save();Changed();Changed(nameof(FavoriteMark));owner.RefreshFavorites();}}
}
public sealed class QuestMaterialRow:Observable,IItemTooltipModel {
 readonly QuestViewModel owner;readonly string key;public QuestMaterial Model {get;}public FarmResult? Best {get;set;}public FarmResult? SpotlightAlternative {get;set;}
 public QuestMaterialRow(QuestMaterial model,int quest,QuestViewModel owner){Model=model;this.owner=owner;key=quest+":"+model.Index;}
 public QuestRow? Dependency=>owner.FindDependency(Model.AegisName);public bool HasDependency=>Dependency!=null;public string DependencyLabel=>Dependency?.Model.Kind=="Normais"?"Obtido através de Quest Normal":"Obtido através de Quest Rara";public ICommand DependencyCommand=>new RelayCommand(_=>owner.NavigateDependency(Model.AegisName));
 public FarmItem? SelectedItem=>Model.ItemId>0?new(){Id=Model.ItemId,Name=Model.Name}:null;public string ItemName=>Model.Label;public string Icon=>Model.ItemId>0?Model.Icon:"pack://application:,,,/Assets/AppIcon.ico";public ItemDescriptionService Descriptions=>owner.Descriptions;
 public bool Obtained {get=>owner.Progress.Obtained.Contains(key);set{if(value)owner.Progress.Obtained.Add(key);else owner.Progress.Obtained.Remove(key);owner.Save();Changed();}}
 public string Quantity=>Model.Amount.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))+"x";
 public string FarmLine=>Best==null?(Model.ItemId<=0?"ID não identificado":"Sem drop comum confirmado"):Best.MonsterName+" · "+Best.ChanceLabel+" · "+(Best.BestMap?.Map??"Mapa não identificado");
 public string MapReference=>Best?.BestMap is not {} map?"":map.Evidence.StartsWith("rAthena",StringComparison.Ordinal)?"Referência Renewal":map.Evidence.StartsWith("Divine Pride",StringComparison.Ordinal)?"Referência Divine Pride":"";
 public bool HasReference=>MapReference.Length>0;
 public string Farm=>Model.ItemId<=0?"ID não identificado na fonte autorizada":Best==null?"Sem drop de monstro comum no banco local":Best.MonsterName+" · "+Best.ChanceLabel+"\n"+Best.MapLabel+"\n"+Best.MapSource;
 public string SpotlightLabel=>SpotlightAlternative==null?"":"☀ No SpotLight atual: "+SpotlightAlternative.MapLabel+" · "+SpotlightAlternative.MonsterName+" · "+SpotlightAlternative.ChanceLabel;
 public bool HasMap=>Best?.HasMap==true;
 public ICommand MapCommand=>new RelayCommand(_=>owner.OpenMap(Best));public ICommand SpotlightMapCommand=>new RelayCommand(_=>owner.OpenMap(SpotlightAlternative));
 public bool HasSpotlight=>SpotlightAlternative!=null;
 public void Refresh(){Changed(nameof(Farm));Changed(nameof(FarmLine));Changed(nameof(MapReference));Changed(nameof(HasReference));Changed(nameof(HasMap));Changed(nameof(SpotlightLabel));Changed(nameof(HasSpotlight));}
}
public sealed class QuestViewModel:Observable {
 readonly PersistenceService store;readonly SpotlightViewModel spotlight;readonly CatalogViewModel integration;readonly List<QuestRow> all=new();readonly Dictionary<int,List<FarmResult>> cache=new();
 public QuestProgress Progress {get;}public ObservableCollection<QuestRow> Results {get;}=new();public ObservableCollection<QuestRow> Favorites {get;}=new();public ObservableCollection<QuestMaterialRow> Materials {get;}=new();
 public string[] Categories {get;}={"Todas","Topo","Meio","Baixo","Escudos","Capas","Acessórios","Armas","Outros"};string query="",category="Todas";QuestRow? selected;int generation;string active="";
 readonly Action? showQuest;public ItemDescriptionService Descriptions {get;}=new();int kindTab;
 public ICommand SelectKindCommand=>new RelayCommand(p=>{if(int.TryParse(p?.ToString(),out int value)&&value is 0 or 1)KindTab=value;});
 public int KindTab {get=>kindTab;set{if(kindTab==value)return;kindTab=value;Changed();Filter();if(selected?.Model.Kind!=(kindTab==0?"Normais":"Raras")){selected=null;generation++;Materials.Clear();Changed(nameof(Selected));Changed(nameof(CompactSelected));Selected=Results.FirstOrDefault();}}}
 public string Query {get=>query;set{query=value;Changed();Filter();}}public string Category {get=>category;set{category=value;Changed();Filter();}}
 public QuestRow? Selected {get=>selected;set{if(value==null||selected==value)return;selected=value;Progress.SelectedId=value.Model.Id;Save();Changed();Changed(nameof(CompactSelected));SelectionTask=LoadMaterials();}}
 public Task SelectionTask {get;private set;}=Task.CompletedTask;
 public QuestRow? CompactSelected {get=>Selected?.Favorite==true?Selected:null;set=>Selected=value;}
 public string Status {get;private set;}="";
 public QuestViewModel(PersistenceService store,SpotlightViewModel spotlight,CatalogViewModel integration,Action? showQuest=null){this.showQuest=showQuest;this.store=store;this.spotlight=spotlight;this.integration=integration;Progress=store.Load<QuestProgress>("quest-progress.json");Progress.Favorites??=new();Progress.Obtained??=new();
  try{all.AddRange(QuestRepository.Load().Select(q=>new QuestRow(q,this)));int unknown=all.Sum(q=>q.Model.Materials.Count(m=>m.ItemId==0));Status=$"{all.Count(q=>q.Model.Kind=="Normais")} normais · {all.Count(q=>q.Model.Kind=="Raras")} raras · progresso salvo"+(unknown>0?$" · {unknown} materiais sem ID no catálogo fornecido":"");}catch{Status="Não foi possível abrir o catálogo de quests.";}
  var restored=all.FirstOrDefault(q=>q.Model.Id==Progress.SelectedId);kindTab=restored?.Model.Kind=="Raras"?1:0;Filter();RefreshFavorites();Selected=restored;spotlight.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(SpotlightViewModel.ConfirmedGroup)){var id=spotlight.Pending?"":spotlight.ConfirmedGroup?.Id??"";if(id!=active){active=id;_=LoadMaterials();}}};
 }
 public string RecipeSuffix(QuestDefinition model){var variants=all.Where(q=>q.Model.Kind==model.Kind&&q.Model.AegisName==model.AegisName).ToList();return variants.Count>1?$" · opção {variants.FindIndex(q=>q.Model.Id==model.Id)+1}/{variants.Count}":"";}
 public QuestRow? FindDependency(string aegis)=>string.IsNullOrEmpty(aegis)?null:all.Where(q=>q.Model.AegisName==aegis&&q!=Selected).OrderBy(q=>q.Model.Kind=="Normais"?0:1).FirstOrDefault();
 public void NavigateDependency(string aegis){var quest=FindDependency(aegis);if(quest==null)return;query="";category="Todas";kindTab=quest.Model.Kind=="Normais"?0:1;Changed(nameof(Query));Changed(nameof(Category));Changed(nameof(KindTab));Filter();Selected=quest;showQuest?.Invoke();}
 public void Save()=>store.Save("quest-progress.json",Progress);
 void Filter(){Results.Clear();var text=MvpSearchCoordinator.Normalize(query);foreach(var row in all.Where(q=>q.Model.Kind==(KindTab==0?"Normais":"Raras")&&(category=="Todas"||q.Category==category)&&MvpSearchCoordinator.Normalize(q.Name+" "+q.Model.ItemId).Contains(text,StringComparison.Ordinal)))Results.Add(row);}
 public void RefreshFavorites(){Favorites.Clear();foreach(var row in all.Where(q=>q.Favorite))Favorites.Add(row);Changed(nameof(CompactSelected));}
 async Task LoadMaterials(){int version=++generation;Materials.Clear();var quest=Selected;if(quest==null)return;var maps=spotlight.Pending?new HashSet<string>():new HashSet<string>(spotlight.ConfirmedGroup?.Maps.Select(m=>m.Code)??Enumerable.Empty<string>(),StringComparer.OrdinalIgnoreCase);
  foreach(var material in quest.Model.Materials){var row=new QuestMaterialRow(material,quest.Model.Id,this);Materials.Add(row);if(material.ItemId<=0)continue;
   try{if(!cache.TryGetValue(material.ItemId,out var ranked)){ranked=await Task.Run(()=>ItemFarmIndex.Shared.Rank(material.ItemId,0,true,true));if(version!=generation)return;foreach(var candidate in ranked.Where(r=>!r.HasMap).Take(5).ToArray()){var resolved=await ItemFarmMapResolver.ResolveAsync(candidate,integration.Api,integration.GatewaySlug,default);if(version!=generation)return;ranked[ranked.IndexOf(candidate)]=resolved;}cache[material.ItemId]=ranked;}
    row.Best=ranked.OrderByDescending(r=>r.HasMap).ThenByDescending(r=>r.Score??-1).ThenByDescending(r=>r.Chance).FirstOrDefault();
    row.SpotlightAlternative=ranked.SelectMany(r=>r.Monster.Spawns.Where(s=>maps.Contains(s.Map)&&(!r.Monster.Spawns.Any(p=>p.Evidence=="Farmland")||s.Evidence=="Farmland")&&(s.Count==null||s.Count>0)).Select(s=>new FarmResult{Monster=r.Monster,Chance=r.Chance,BestMap=s})).OrderByDescending(r=>r.Score??-1).ThenByDescending(r=>r.Chance).FirstOrDefault();row.Refresh();
   }catch{Status="Alguns mapas não puderam ser complementados; dados locais preservados.";Changed(nameof(Status));}
  }
 }
 public void OpenMap(FarmResult? row){if(row?.BestMap is not {} map||!System.Text.RegularExpressions.Regex.IsMatch(map.Map,"^[a-z0-9_]+$"))return;try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.divine-pride.net/database/map/"+map.Map){UseShellExecute=true});}catch{Status="Não foi possível abrir o navegador.";Changed(nameof(Status));}}
}
