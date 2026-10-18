using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class ItemFarmMapResolver {
 public static async Task<FarmResult> ResolveAsync(FarmResult row,IDivinePrideApiClient api,string region,CancellationToken ct){
  if(!row.Farmable||row.BestMap?.Evidence=="Farmland"||!api.CanQuery||PublicRegularSpawns.Shared.ForMonster(row.Monster.Id).Count==0)return row;
  try{
   var monster=await api.MonsterSpawnsAsync(region,row.Monster.Id,ct);ct.ThrowIfCancellationRequested();
   var regular=(monster.MapCandidates??new()).Where(s=>PublicRegularSpawns.Shared.Regular(row.Monster.Id,s.Map)&&(s.Count==null||s.Count>0)).GroupBy(s=>s.Map).Select(g=>g.OrderByDescending(s=>s.Count??-1).First()).ToList();
   if(regular.Count==0)return row;foreach(var spawn in regular)spawn.Evidence="Divine Pride — "+region+" (regularidade cruzada com spawns públicos Renewal; não confirmado no Farmland)";
   row.Monster.Spawns=regular;return new FarmResult{Monster=row.Monster,Chance=row.Chance,BestMap=regular.OrderByDescending(s=>s.Count??-1).ThenBy(s=>s.Map).First()};
  }catch(OperationCanceledException){throw;}catch(DivinePrideException){return row;}
 }
}
