using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public interface IDivinePrideCatalogProvider {
 Task<List<CatalogEntry>> DetailsAsync(string region,int id,bool refresh=false,CancellationToken ct=default);
}
public sealed class DivinePrideCatalogProvider:IDivinePrideCatalogProvider {
 readonly IDivinePrideApiClient api;
 public DivinePrideCatalogProvider(IDivinePrideApiClient api)=>this.api=api;
 public async Task<List<CatalogEntry>> DetailsAsync(string region,int id,bool refresh=false,CancellationToken ct=default){
  var monster=await api.MonsterAsync(region,id,refresh,ct);var points=new MvpRespawnCatalog().Records.Where(r=>r.MonsterId==id).ToList();
  if(points.Count==0&&monster.Exp?.Mvp is not >0)throw new DivinePrideException("Os detalhes não confirmam este ID como MVP. Os favoritos existentes foram preservados.");
  var maps=points.Select(r=>r.MapCode).Distinct().DefaultIfEmpty("");
  return maps.Select(map=>new CatalogEntry{Key=$"divine:{region}:{id}:{map}",MonsterId=id,Name=monster.Name!,Map=map,Gateway="dp:"+region,Origin="Divine Pride — "+region,SourceUrl=monster.Url??"https://www.divine-pride.net",SyncedUtc=DateTimeOffset.UtcNow,Notes="Detalhes Divine Pride. A documentação não especifica os campos/unidades de spawns; o respawn usa o catálogo Renewal/Farmland verificado."}).ToList();
 }
}