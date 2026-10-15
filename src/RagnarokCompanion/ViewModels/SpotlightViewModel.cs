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
 public ObservableCollection<SpotlightGroup> Groups { get; }=new();
 public ObservableCollection<SpotlightRow> Rows { get; }=new();
 public ICollectionView Maps { get; }
 public SpotlightLevelFilter[] Filters { get; }={new("Todos",null,null),new("1–49",1,49),new("50–79",50,79),new("80–99",80,99),new("100–129",100,129),new("130+",130,999)};
 public SpotlightCatalog? Catalog { get; private set; }
 public string Message { get; private set; }="Carregue o catálogo oficial para consultar os mapas.";
 public SpotlightViewModel(PersistenceService store, ISpotlightCatalogService? service=null, Func<DateTimeOffset>? clock=null)
 {
  this.store=store; this.service=service??new SpotlightCatalogService(store); this.clock=clock??(()=>DateTimeOffset.UtcNow);
  selection=store.Load<SpotlightSelection>("spotlight-selection.json"); filter=Filters[0]; Maps=CollectionViewSource.GetDefaultView(Rows);
  RefreshCommand=new RelayCommand(async _=>await LoadAsync(true)); ConfirmCommand=new RelayCommand(_=>Confirm());
  OpenMapCommand=new RelayCommand(p=>{if(p is SpotlightMap map) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(map.DivinePrideUrl){UseShellExecute=true});});
  Apply(this.service.LoadLocal());
 }
 public ICommand RefreshCommand { get; } public ICommand ConfirmCommand { get; } public ICommand OpenMapCommand { get; }
 public SpotlightGroup? SelectedGroup { get=>selected; set {if(selected==value)return; selected=value; if(value!=null){selection.GroupId=value.Id; selection.SelectedUtc=clock(); Save();} Rebuild(); Changed(); Tick();} }
 public string Search {get=>search;set{search=value;Changed();Rebuild();}}
 public SpotlightLevelFilter Filter {get=>filter;set{filter=value??Filters[0];Changed();Rebuild();}}
 public int? CharacterLevel {get=>selection.CharacterLevel;set{selection.CharacterLevel=value is >=1 and <=999?value:null;Save();Changed();Rebuild();}}
 public bool ShowCompact {get=>selection.ShowCompact;set{selection.ShowCompact=value;Save();Changed();}}
 public string Countdown=>SpotlightRotationService.Countdown(clock());
 public bool Pending=>selected==null||SpotlightRotationService.IsPending(selection.SelectedUtc,clock());
 public string Status=>Pending?"Pendente de confirmação do jogador":"Grupo informado pelo jogador";
 public string Confirmation=>selection.SelectedUtc is {} time?"Informado em "+TimeZoneInfo.ConvertTime(time,SpotlightRotationService.ServerZone).ToString("dd/MM/yyyy HH:mm:ss")+" (São Paulo)":"Nenhum grupo informado";
 public string Summary=>selected?.Summary??"Selecione o Spotlight atual";
 public string Count=>selected==null?"Nenhum grupo selecionado":$"{Rows.Count} de {selected.Maps.Count} mapas • faixa {selected.Maps.Min(m=>m.MinLevel)}–{selected.Maps.Max(m=>m.MaxLevel)}";
 public string Source=>Catalog==null?"Fonte: wiki oficial Farmland":$"Wiki Farmland • revisão {Catalog.Revision} • cache de {Catalog.RetrievedUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
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
  selected=Groups.FirstOrDefault(g=>g.Id==selection.GroupId);Message="Catálogo local disponível offline.";Rebuild();Changed(nameof(SelectedGroup));Changed(nameof(Source));Tick();
 }
 void Rebuild()
 {
  Rows.Clear();
  foreach(var map in selected?.Maps??Enumerable.Empty<SpotlightMap>())
   if((map.Code+" "+map.Name).Contains(search.Trim(),StringComparison.OrdinalIgnoreCase)&&(filter.Min==null||map.Recommended>=filter.Min)&&(filter.Max==null||map.Recommended<=filter.Max))Rows.Add(new(map,CharacterLevel));
  Changed(nameof(Count));Changed(nameof(Summary));
 }
 public void Confirm(){if(selected==null)return;selection.SelectedUtc=clock();Save();Tick();}
 void Save()=>store.Save("spotlight-selection.json",selection);
 public void Tick(){Changed(nameof(Countdown));Changed(nameof(Pending));Changed(nameof(Status));Changed(nameof(Confirmation));}
 public void Dispose(){if(service is IDisposable disposable)disposable.Dispose();}
}
