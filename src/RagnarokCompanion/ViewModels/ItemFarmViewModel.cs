using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed class ItemFarmViewModel:Observable,IDisposable,IItemTooltipModel {
 readonly PersistenceService store;readonly CatalogViewModel integration;readonly ItemFarmPreferences preferences;readonly Dictionary<int,string> names;
 CancellationTokenSource? searchRequest,mapRequest;int rankVersion;bool disposed;
 string query="",status="Pesquise um nome ou ID para encontrar locais de farm.",itemName="Nenhum item selecionado",icon="";FarmItem? selected;FarmResult? best;bool busy;
 public ObservableCollection<FarmItem> Suggestions {get;}=new();public ObservableCollection<FarmResult> Others {get;}=new();public ObservableCollection<FarmResult> Excluded {get;}=new();
 public double[] MinimumOptions {get;}={0,1,5,10,20,50};public string[] SortOptions {get;}={"Maior chance","Melhor farm"};
 public string Query {get=>query;set{if(query==value)return;query=value??"";Changed();searchRequest?.Cancel();_=SearchSuggestionsAsync();}}
 public ItemDescriptionService Descriptions {get;}=new();
 public Task SelectionTask {get;private set;}=Task.CompletedTask;
 public FarmItem? SelectedItem {get=>selected;set{if(value==null||selected?.Id==value.Id)return;selected=value;Changed();SelectionTask=SelectAsync(value);}}
 public double Minimum {get=>preferences.Minimum;set{if(!MinimumOptions.Contains(value))return;preferences.Minimum=value;Changed();Save();_=RankAsync();}}
 public string Sort {get=>preferences.Sort;set{preferences.Sort=SortOptions.Contains(value)?value:SortOptions[0];Changed();Save();_=RankAsync();}}
 public bool FarmableOnly {get=>preferences.FarmableOnly;set{preferences.FarmableOnly=value;Changed();Changed(nameof(IncludeSpecial));Save();_=RankAsync();}}
 public bool IncludeSpecial {get=>!FarmableOnly;set=>FarmableOnly=!value;}
 public string ItemName {get=>itemName;private set{itemName=value;Changed();}}
 public string Icon {get=>icon;private set{icon=value;Changed();}}
 public string Status {get=>status;private set{status=value;Changed();}}
 public bool Busy {get=>busy;private set{busy=value;Changed();}}
 public FarmResult? Best {get=>best;private set{best=value;Changed();Changed(nameof(HasBest));}}
 public bool HasBest=>Best!=null;bool lower;public bool OfferLower {get=>lower;private set{lower=value;Changed();}}
 public ICommand OpenItemCommand {get;}
 public bool HasItem=>SelectedItem!=null;
 public string? ItemPublicUrl {get {
  if(SelectedItem==null)return null;
  string region=integration.GatewaySlug switch{"GGH"=>"GGHRO","twROZ"=>"twRO zero",_=>integration.GatewaySlug};
  string language=integration.Language switch{"pt"=>"Portuguese","en"=>"English","ko"=>"Korean","ja"=>"Japanese","ru"=>"Russian","fr"=>"French","de"=>"German","es"=>"Spanish","th"=>"Thai","cn"=>"Chinese",_=>"Portuguese"};
  return "https://www.divine-pride.net/database/item/"+SelectedItem.Id+"#:~:text="+Uri.EscapeDataString(region+" - "+language);
 }}
 public ICommand SearchCommand {get;}public ICommand LowerCommand {get;}public ICommand MapCommand {get;}public ICommand MonsterCommand {get;}
 public ItemFarmViewModel(PersistenceService store,CatalogViewModel integration){this.store=store;this.integration=integration;preferences=store.Load<ItemFarmPreferences>("item-farm-settings.json");names=store.Load<Dictionary<int,string>>("item-farm-names.json");if(!MinimumOptions.Contains(preferences.Minimum))preferences.Minimum=10;if(!SortOptions.Contains(preferences.Sort))preferences.Sort=SortOptions[0];
  SearchCommand=new AsyncCommand(async()=>{var text=Query;var list=await Task.Run(()=>ItemFarmIndex.Shared.Search(text));if(disposed||text!=Query)return;if(int.TryParse(text,out int id)&&id>0){SelectedItem=list.FirstOrDefault();if(SelectedItem==null)Status="Item não encontrado no banco local.";}else if(Suggestions.Count==1)SelectedItem=Suggestions[0];else if(list.Count==1)SelectedItem=list[0];else Status="Selecione um item nas sugestões.";});
  OpenItemCommand=new RelayCommand(_=>{if(ItemPublicUrl is {} url)try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{Status="Não foi possível abrir o navegador.";}});
  LowerCommand=new RelayCommand(_=>Minimum=0);MapCommand=new RelayCommand(OpenMap);
  MonsterCommand=new RelayCommand(p=>{if(p is FarmResult row)_=MonsterAsync(row);});
 }
 void OpenMap(object? parameter){if(parameter is not FarmResult row||row.BestMap is not {} map||!System.Text.RegularExpressions.Regex.IsMatch(map.Map,"^[a-z0-9_]+$"))return;try{Process.Start(new ProcessStartInfo("https://www.divine-pride.net/database/map/"+map.Map){UseShellExecute=true});}catch{Status="Não foi possível abrir o navegador.";}}
 void Save(){if(!disposed)store.Save("item-farm-settings.json",preferences);}
 async Task SearchSuggestionsAsync(){var source=new CancellationTokenSource();searchRequest=source;var text=Query;try{await Task.Delay(150,source.Token);var result=await Task.Run(()=>ItemFarmIndex.Shared.Search(text),source.Token);source.Token.ThrowIfCancellationRequested();if(disposed)return;
  var normalized=MvpSearchCoordinator.Normalize(text);if(normalized.Length>0)foreach(var pair in names.Where(p=>MvpSearchCoordinator.Normalize(p.Value).Contains(normalized,StringComparison.Ordinal)).Take(20)){if(result.All(i=>i.Id!=pair.Key)&&ItemFarmIndex.Shared.Find(pair.Key) is {} known)result.Add(known);}
  Suggestions.Clear();foreach(var item in result.Take(20))Suggestions.Add(item);if(result.Count==1)SelectedItem=result[0];
 }catch(OperationCanceledException){}catch{if(!disposed)Status="Não foi possível carregar o catálogo local.";}finally{if(ReferenceEquals(searchRequest,source))searchRequest=null;source.Dispose();}}
 async Task SelectAsync(FarmItem item){ItemName=item.Label;Icon="https://static.divine-pride.net/images/items/item/"+item.Id+".png";preferences.SelectedId=item.Id;Save();Changed(nameof(HasItem));Changed(nameof(ItemPublicUrl));await RankAsync();}
 public async Task RankAsync(){var item=SelectedItem;if(item==null)return;int version=++rankVersion;mapRequest?.Cancel();var source=new CancellationTokenSource();mapRequest=source;var min=Minimum;var farmable=FarmableOnly;var bestFarm=Sort=="Melhor farm";
  try{var rows=await Task.Run(()=>ItemFarmIndex.Shared.Rank(item.Id,min,farmable,bestFarm),source.Token);if(disposed||version!=rankVersion)return;Present(rows,item.Id,bestFarm,min);Busy=integration.Api.CanQuery;
   foreach(var row in rows.Where(r=>r.Farmable).OrderByDescending(r=>r.Chance).Take(5).ToArray()){source.Token.ThrowIfCancellationRequested();var resolved=await ItemFarmMapResolver.ResolveAsync(row,integration.Api,integration.GatewaySlug,source.Token);if(disposed||version!=rankVersion)return;rows[rows.IndexOf(row)]=resolved;}
   rows=ItemFarmMapResolver.Order(rows,bestFarm);Present(rows,item.Id,bestFarm,min);
  }catch(OperationCanceledException){}catch{if(!disposed)Status="Não foi possível atualizar os mapas; dados locais preservados.";}
  finally{if(ReferenceEquals(mapRequest,source)){mapRequest=null;Busy=false;}source.Dispose();}}
 public bool HasOthers=>Others.Count>0;
 void Present(List<FarmResult> rows,int item,bool bestFarm,double min){var visible=ItemFarmMapResolver.Order(rows.Where(r=>r.Farmable||IncludeSpecial),bestFarm);Best=visible.FirstOrDefault(r=>r.Farmable&&r.HasMap);Others.Clear();foreach(var row in visible.Where(r=>!ReferenceEquals(r,Best)).Take(Best==null?5:4))Others.Add(row);Changed(nameof(HasOthers));Excluded.Clear();OfferLower=Best==null&&min>0&&ItemFarmIndex.Shared.Rank(item,0,FarmableOnly,bestFarm).Any(r=>r.Farmable&&r.HasMap);Status=Best!=null?"Recomendação com mapa regular de referência. Drops Farmland; confira a origem do mapa.":OfferLower?"Mostre chances menores para encontrar opções com mapa conhecido.":visible.Count>0?"Sem spawn regular confirmado para recomendar. Confira as outras opções.":"Nenhum monstro atende aos filtros atuais.";}
 async Task MonsterAsync(FarmResult row){try{var result=await integration.Api.MonsterAsync(integration.GatewaySlug,row.Monster.Id);if(!disposed)Status=$"Divine Pride: {result.Name}"+(result.Level is {} level?$" · nível {level}":"")+". Drops usam o banco Farmland; mapas dependem de spawns importados.";}catch(DivinePrideException ex){if(!disposed)Status=ex.Message;}catch{if(!disposed)Status="Detalhes indisponíveis; ranking local preservado.";}}
 public void Restore(){if(SelectedItem==null&&preferences.SelectedId>0){var id=preferences.SelectedId;_=RestoreAsync(id);}}
 async Task RestoreAsync(int id){var item=await Task.Run(()=>ItemFarmIndex.Shared.Find(id));if(!disposed&&SelectedItem==null&&item!=null)SelectedItem=item;}
 public void Dispose(){disposed=true;searchRequest?.Cancel();mapRequest?.Cancel();}
}
