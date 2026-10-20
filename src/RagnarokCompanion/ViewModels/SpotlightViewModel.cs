using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed class SpotlightRow
{
 public SpotlightMap Map { get; }
 public SpotlightRow(SpotlightMap map, int? level) { Map=map; Suitable=level.HasValue && map.MinLevel<=level && map.MaxLevel>=level; }
 public bool Suitable { get; }
}
public sealed class SpotlightViewModel : Observable, IDisposable
{
 readonly PersistenceService store; readonly ISpotlightCatalogService service; readonly Func<DateTimeOffset> clock;
 readonly SpotlightSelection selection; bool loading; SpotlightGroup? selected; string search=""; SpotlightLevelFilter filter;
 string? lastActiveId;
 public ObservableCollection<SpotlightGroup> Groups { get; }=new();
 public ObservableCollection<SpotlightRow> Rows { get; }=new();
 public ICollectionView Maps { get; }
 public SpotlightLevelFilter[] Filters { get; }={new("Todos",null,null),new("1–49",1,49),new("50–79",50,79),new("80–99",80,99),new("100–129",100,129),new("130+",130,999)};
 public SpotlightCatalog? Catalog { get; private set; }
 public string Message { get; private set; }="Carregue o catálogo oficial para consultar os mapas.";
 public SpotlightViewModel(PersistenceService store, ISpotlightCatalogService? service=null, Func<DateTimeOffset>? clock=null)
 {
  this.store=store; this.service=service??new SpotlightCatalogService(store,preferBundled:store.IsDefaultLocation); this.clock=clock??(()=>DateTimeOffset.UtcNow);
  selection=store.Load<SpotlightSelection>("spotlight-selection.json");selection.RotationOrder??=new(); filter=Filters[0]; Maps=CollectionViewSource.GetDefaultView(Rows);
  RefreshCommand=new RelayCommand(async _=>await LoadAsync(true)); ConfirmCommand=new RelayCommand(_=>Confirm());
  OpenMapCommand=new RelayCommand(p=>{if(p is SpotlightMap { HasMapLink: true } map) {try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(map.MapUrl!){UseShellExecute=true});}catch{Message="Não foi possível abrir o navegador.";Changed(nameof(Message));}}});
  ClearFiltersCommand=new RelayCommand(_=>{Search="";Filter=Filters[0];CharacterLevel=null;});
  Apply(this.service.LoadLocal());
 }
 public ICommand ClearFiltersCommand { get; }
 public ICommand RefreshCommand { get; } public ICommand ConfirmCommand { get; } public ICommand OpenMapCommand { get; }
 public SpotlightGroup? SelectedGroup { get=>selected; set {if(selected==value)return; selected=value; if(value!=null){selection.ViewedGroupId=value.Id; Save();} Rebuild(); Changed(); Tick();} }
 public string Search {get=>search;set{search=value;Changed();Rebuild();}}
 public SpotlightLevelFilter Filter {get=>filter;set{filter=value??Filters[0];Changed();Rebuild();}}
 public int? CharacterLevel {get=>selection.CharacterLevel;set{selection.CharacterLevel=value is >=1 and <=999?value:null;Save();Changed();Rebuild();}}
 public bool ShowCompact {get=>selection.ShowCompact;set{selection.ShowCompact=value;Save();Changed();}}
 public string Countdown=>SpotlightRotationService.Countdown(clock());
 string? rotationDraft;
 public string RotationHelp=>"Grupos disponíveis: "+string.Join(" · ",Groups.Select(g=>g.Name));
 public string RotationDraft {get=>rotationDraft??(selection.RotationOrder.Count==0?"Celestial → Infernal → Astral → Solar":string.Join(" → ",selection.RotationOrder.Select(id=>Groups.FirstOrDefault(g=>g.Id==id)?.Name??id)));set{rotationDraft=value;Changed();}}
 public ICommand SaveRotationCommand=>new RelayCommand(_=>{
  var names=RotationDraft.Split(new[]{"→",",",";"},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
  var order=names.Select(n=>Groups.FirstOrDefault(g=>g.Name.Equals(n,StringComparison.OrdinalIgnoreCase)||g.Id.Equals(n,StringComparison.OrdinalIgnoreCase)||g.Name.Equals("Spotlight "+n,StringComparison.OrdinalIgnoreCase))).ToArray();
  if(order.Length!=Groups.Count||order.Any(g=>g==null)||order.Select(g=>g!.Id).Distinct().Count()!=Groups.Count){Message="Informe todos os sete grupos, uma vez cada, na ordem confirmada pelo servidor.";Changed(nameof(Message));return;}
  var current=HasRotation&&!Pending?ConfirmedGroup:null;selection.RotationOrder=order.Select(g=>g!.Id).ToList();if(current!=null){selection.GroupId=current.Id;selection.SelectedUtc=clock();}Save();Message="Sequência salva. Corrija o grupo atual se necessário.";Changed(nameof(Message));Tick();
 });
 public bool HasRotation=>selection.RotationOrder.Count==Groups.Count&&selection.RotationOrder.Distinct().Count()==Groups.Count&&selection.RotationOrder.All(id=>Groups.Any(g=>g.Id==id));
 public SpotlightGroup? ConfirmedGroup {get{string id=selection.GroupId;if(HasRotation&&selection.SelectedUtc is {} reference&&reference<=clock()){int start=selection.RotationOrder.IndexOf(id);if(start>=0)id=selection.RotationOrder[(start+SpotlightRotationService.Steps(reference,clock()))%selection.RotationOrder.Count];}return Groups.FirstOrDefault(g=>g.Id==id);}}
 public bool Pending=>ConfirmedGroup==null||selection.SelectedUtc>clock()||(!HasRotation&&SpotlightRotationService.IsPending(selection.SelectedUtc,clock()));
 public string Status=>ConfirmedGroup==null?"Informe o grupo atual":Pending?"Configure a sequência completa para acompanhar a rotação":(HasRotation&&selection.SelectedUtc is {} reference&&SpotlightRotationService.Steps(reference,clock())>0?"Calculado pela sequência: ":"Confirmado por você: ")+ConfirmedGroup.Name;
 public string Confirmation=>selection.SelectedUtc is {} time?"Informado em "+TimeZoneInfo.ConvertTime(time,SpotlightRotationService.ServerZone).ToString("dd/MM/yyyy HH:mm:ss")+" (São Paulo)":"Nenhum grupo informado";
 public string Summary=>ConfirmedGroup?.Summary??"Nenhum Spotlight confirmado";
 public string Count=>selected==null?"Selecione um grupo":Rows.Count==selected.Maps.Count?$"{selected.Maps.Count} mapas":$"{Rows.Count} de {selected.Maps.Count} mapas";
 public string Source=>Catalog==null?"Fonte: wiki oficial Farmland":$"{Catalog.DataOrigin} • revisão {(Catalog.Revision>0?Catalog.Revision.ToString():"não informada")} • {Catalog.RetrievedUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
 public async Task LoadAsync(bool force=false)
 {
  if(loading||(!force&&Catalog!=null))return; loading=true; Message="Consultando a página oficial…";Changed(nameof(Message));
  try{Apply(await service.RefreshAsync());Message="Catálogo local disponível offline.";}
  catch(Exception){Message=Catalog==null?"Não foi possível carregar a wiki. Tente atualizar quando houver conexão.":"Atualização indisponível. O catálogo local foi preservado.";}
  finally{loading=false;Changed(nameof(Message));}
 }
 void Apply(SpotlightCatalog? catalog)
 {
  if(catalog==null)return; Catalog=catalog;Groups.Clear();foreach(var g in catalog.Groups)Groups.Add(g);
  if(selection.RotationOrder.Count==0&&SpotlightRotationService.OfficialOrder.All(id=>Groups.Any(g=>g.Id==id))){selection.RotationOrder=SpotlightRotationService.OfficialOrder.ToList();Save();}
  selected=Groups.FirstOrDefault(g=>g.Id==(string.IsNullOrEmpty(selection.ViewedGroupId)?selection.GroupId:selection.ViewedGroupId))??Groups.FirstOrDefault();Message="Catálogo local disponível offline.";Rebuild();Changed(nameof(SelectedGroup));Changed(nameof(Source));Tick();
 }
 void Rebuild()
 {
  Rows.Clear();
  foreach(var map in selected?.Maps??Enumerable.Empty<SpotlightMap>())
   if(MvpSearchCoordinator.Normalize(map.Code+" "+map.Name).Contains(MvpSearchCoordinator.Normalize(search.Trim()),StringComparison.Ordinal)&&(filter.Min==null||map.Recommended>=filter.Min)&&(filter.Max==null||map.Recommended<=filter.Max))Rows.Add(new(map,CharacterLevel));
  Changed(nameof(Count));Changed(nameof(Summary));
 }
 public void Confirm(){if(selected==null)return;selection.GroupId=selected.Id;selection.SelectedUtc=clock();Save();Tick();}
 void Save()=>store.Save("spotlight-selection.json",selection);
 public void Tick(){
  var active=ConfirmedGroup;
  if(!Pending&&active!=null){
   bool rotated=lastActiveId!=null?lastActiveId!=active.Id:HasRotation&&selection.SelectedUtc is {} reference&&SpotlightRotationService.Steps(reference,clock())>0;
   lastActiveId=active.Id;
   if(rotated&&selected?.Id!=active.Id){selected=active;selection.ViewedGroupId=active.Id;Save();Rebuild();Changed(nameof(SelectedGroup));}
  }
  Changed(nameof(Countdown));Changed(nameof(Pending));Changed(nameof(Status));Changed(nameof(Confirmation));Changed(nameof(Summary));Changed(nameof(ConfirmedGroup));
 }
 public void Dispose(){if(service is IDisposable disposable)disposable.Dispose();}
}
