using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class ItemFarmMapResolver {
 static bool Usable(FarmSpawn spawn)=>System.Text.RegularExpressions.Regex.IsMatch(spawn.Map,"^[a-z0-9_]+$")&&(spawn.Count==null||spawn.Count>0)&&!string.IsNullOrWhiteSpace(spawn.Evidence);
 public static FarmResult ResolveLocal(FarmResult row){
  if(!row.Farmable)return row;
  var maps=row.Monster.Spawns.Where(Usable).Where(s=>!s.Evidence.StartsWith("rAthena",StringComparison.Ordinal)||PublicRegularSpawns.Shared.Regular(row.Monster.Id,s.Map)).Concat(PublicRegularSpawns.Shared.ForMonster(row.Monster.Id)).Where(Usable)
   .GroupBy(s=>s.Map,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(s=>s.Evidence=="Farmland").ThenByDescending(s=>s.Evidence.StartsWith("Divine Pride",StringComparison.Ordinal)).ThenByDescending(s=>s.Count??-1).First()).ToList();
  row.Monster.Spawns=maps;var available=maps.Any(s=>s.Evidence=="Farmland")?maps.Where(s=>s.Evidence=="Farmland"):maps;
  return new FarmResult{Monster=row.Monster,Chance=row.Chance,BestMap=available.OrderByDescending(s=>s.Count??-1).ThenBy(s=>s.Map,StringComparer.Ordinal).FirstOrDefault()};
 }
 public static List<FarmResult> Order(IEnumerable<FarmResult> rows,bool bestFarm)=> (bestFarm?rows.OrderByDescending(r=>r.Farmable).ThenByDescending(r=>r.HasMap).ThenByDescending(r=>r.Score??-1).ThenByDescending(r=>r.Chance):rows.OrderByDescending(r=>r.Farmable).ThenByDescending(r=>r.HasMap).ThenByDescending(r=>r.Chance).ThenByDescending(r=>r.BestMap?.Count??-1)).ThenBy(r=>r.Monster.Id).ToList();
 public static async Task<FarmResult> ResolveAsync(FarmResult row,IDivinePrideApiClient api,string region,CancellationToken ct){
  row=ResolveLocal(row);if(!row.Farmable||row.BestMap?.Evidence=="Farmland"||!api.CanQuery)return row;
  try{
   var monster=await api.MonsterSpawnsAsync(region,row.Monster.Id,ct);ct.ThrowIfCancellationRequested();
   var regular=(monster.MapCandidates??new()).Where(s=>PublicRegularSpawns.Shared.Regular(row.Monster.Id,s.Map)&&(s.Count==null||s.Count>0)).GroupBy(s=>s.Map).Select(g=>g.OrderByDescending(s=>s.Count??-1).First()).ToList();
   foreach(var spawn in regular)spawn.Evidence="Divine Pride — "+region+" (regularidade cruzada com spawns públicos Renewal; não confirmado no Farmland)";
   row.Monster.Spawns.InsertRange(0,regular);return ResolveLocal(row);
  }catch(OperationCanceledException){throw;}catch(DivinePrideException){return row;}
 }
}
