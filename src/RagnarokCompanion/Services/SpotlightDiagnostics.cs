using System;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Services;
public static class SpotlightDiagnostics
{
 public static async Task<string> CheckAsync()
 {
  using var source=new SpotlightCatalogService(new PersistenceService());
  var catalog=await source.RefreshAsync();
  var expected=new[]{39,37,39,39,38,38,38};
  if(!catalog.Groups.Select(g=>g.Maps.Count).SequenceEqual(expected))throw new Exception("Contagens da fonte mudaram; revisar a wiki.");
  Verify(catalog);
  return "PASS: fonte oficial; sete grupos; Lunar 37; troca; busca nome/código; filtro recomendado; ordenação; destaque; seleção persistida; cache offline; expiração às 20h sem trocar grupo.\n"+string.Join("\n",catalog.Groups.Select(g=>g.Summary))+"\nFonte: "+catalog.SourceUrl+"\nRevisão: "+catalog.Revision;
 }
 public static void Verify(SpotlightCatalog catalog)
 {
  var root=Path.Combine(Path.GetTempPath(),"Farmland-Spotlight-check-"+Guid.NewGuid());
  var store=new PersistenceService(root);store.Save("spotlight-catalog.json",catalog);
  var time=new DateTimeOffset(2026,10,15,22,59,59,TimeSpan.Zero);
  using var vm=new SpotlightViewModel(store,clock:()=>time);
  var lunar=vm.Groups.FirstOrDefault(g=>g.Id=="lunar")??vm.Groups.First();vm.SelectedGroup=lunar;
  if(vm.Rows.Count!=lunar.Maps.Count||!vm.Pending)throw new Exception("Consulta deve mostrar mapas sem confirmar o grupo.");
  vm.Confirm();
  var sample=lunar.Maps.First();vm.Search=sample.Code;if(!vm.Rows.Any(r=>r.Map.Code==sample.Code))throw new Exception("Busca por código falhou.");
  vm.Search=sample.Name;if(!vm.Rows.Any(r=>r.Map.Code==sample.Code))throw new Exception("Busca por nome falhou.");
  vm.Search="";vm.Filter=vm.Filters[3];if(vm.Rows.Any(r=>r.Map.Recommended is <80 or >99))throw new Exception("Filtro falhou.");
  vm.Filter=vm.Filters[0];vm.Maps.SortDescriptions.Add(new("Map.Recommended",ListSortDirection.Descending));
  var levels=vm.Maps.Cast<SpotlightRow>().Select(r=>r.Map.Recommended).ToArray();if(!levels.SequenceEqual(levels.OrderByDescending(x=>x)))throw new Exception("Ordenação falhou.");
  vm.CharacterLevel=sample.MinLevel;if(sample.MinLevel!=null&&!vm.Rows.First(r=>r.Map.Code==sample.Code).Suitable)throw new Exception("Destaque falhou.");
  using var reload=new SpotlightViewModel(store,clock:()=>time);if(reload.SelectedGroup?.Id!=lunar.Id||reload.Rows.Count!=lunar.Maps.Count)throw new Exception("Persistência offline falhou.");
  time=time.AddSeconds(1);if(!reload.Pending||reload.SelectedGroup?.Id!=lunar.Id||reload.Countdown!="24:00:00")throw new Exception("Rotação falhou.");
  reload.Confirm();if(reload.Pending)throw new Exception("Reconfirmação falhou.");
  foreach(var group in reload.Groups){reload.SelectedGroup=group;if(reload.Rows.Count!=group.Maps.Count)throw new Exception("Troca de grupo falhou.");}
 }
 public static SpotlightCatalog Synthetic()
 {
  var catalog=new SpotlightCatalog{RetrievedUtc=DateTimeOffset.UtcNow};
  foreach(var id in new[]{"solar","lunar"})catalog.Groups.Add(new SpotlightGroup{Id=id,Name="Spotlight "+id+" (teste)",Maps=new(){new SpotlightMap{GroupId=id,Code="test_map",Name="Mapa sintético de teste",MinLevel=80,MaxLevel=100,Recommended=85,Monsters=new(){new SpotlightMonster{Name="Monstro de teste",Level=90}}},new SpotlightMap{GroupId=id,Code="test_map2",Name="Segundo mapa de teste",MinLevel=100,MaxLevel=130,Recommended=115}}});
  return catalog;
 }
}
