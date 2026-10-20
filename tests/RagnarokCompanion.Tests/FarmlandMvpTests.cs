using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class FarmlandMvpTests {
 [Fact] public void OfficialSecondsVariationsAndMapsAreIndependent(){
  var records=new MvpRespawnCatalog().Records.Where(r=>r.IsFarmlandConfirmed).ToList();Assert.Equal(55,records.Count);var snake=records.Single(r=>r.MonsterName=="Evil Snake Lord");Assert.Equal(5650,snake.RespawnMinSeconds);Assert.Equal(6250,snake.RespawnMaxSeconds);Assert.Equal(7000,records.Single(r=>r.MonsterName=="White Lady").RespawnMinSeconds);Assert.Equal(5460,records.Single(r=>r.MonsterName=="Samurai Specter").RespawnMinSeconds);
  Assert.All(records.Where(r=>r.MonsterName is "Leak" or "Scaraba Queen" or "Gold Queen Scaraba"),r=>Assert.Equal(r.RespawnMinSeconds,r.RespawnMaxSeconds));Assert.Equal(1800,records.Single(r=>r.MonsterName=="Kraken").RespawnVarianceSeconds);Assert.Equal(4,records.Count(r=>r.MonsterId==1785));Assert.Equal(2,records.Count(r=>r.MonsterId==1272));
 }
 [Fact] public void CardsRegisterOneClickAndManualTimeWithoutChangingFavorites(){
  var now=new DateTimeOffset(new DateTime(2026,10,20,12,0,0),TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026,10,20)));var store=new PersistenceService(Path.Combine(Path.GetTempPath(),"Farmland-mvp-cards-"+Guid.NewGuid()));var vm=new MainViewModel(store,()=>now,_=>true);
  try{var c=vm.Catalog;var points=c.Cards.Where(r=>r.Entry.MonsterId==1785).ToArray();Assert.Equal(4,points.Length);points[0].FavoriteCommand.Execute(null);points[1].FavoriteCommand.Execute(null);points[0].NowCommand.Execute(null);points[1].TimeCommand.Execute(null);c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);Assert.Equal(2,vm.Engine.Mvps.Count);Assert.Equal(now,vm.Engine.Mvps.Single(m=>m.CatalogKey==points[0].Entry.Key).DeathUtc);Assert.NotEqual(vm.Engine.Mvps[0].CatalogKey,vm.Engine.Mvps[1].CatalogKey);Assert.Equal(2,c.CompactFavorites.Count);c.ClearTimers();Assert.Equal(2,c.FavoriteEntries.Count);Assert.All(vm.Engine.Mvps,m=>Assert.Null(m.DeathUtc));}
  finally{vm.Catalog.Dispose();vm.Spotlight.Dispose();}
 }
 [Fact] public void ExistingFavoriteAndDeathAdoptOfficialScheduleAndKeepTheirKeys(){
  var store=new PersistenceService(Path.Combine(Path.GetTempPath(),"Farmland-mvp-migrate-"+Guid.NewGuid()));const string key="old:atroce:ra_fild03";var death=DateTimeOffset.UtcNow.AddMinutes(-30);store.Save("catalog-choices.json",new[]{new CatalogEntry{Key=key,MonsterId=1785,Name="Atroce",Map="ra_fild03",Favorite=true,Retained=true}});store.Save("mvps.json",new[]{new Mvp{CatalogKey=key,Name="Atroce",Map="ra_fild03",MinMinutes=99,MaxMinutes=100,DeathUtc=death,Origin="Referência antiga"}});
  var vm=new MainViewModel(store);try{var favorite=Assert.Single(vm.Catalog.FavoriteEntries);Assert.Equal(key,favorite.Key);var timer=Assert.Single(vm.Engine.Mvps);Assert.Equal(death,timer.DeathUtc);Assert.Equal(180,timer.MinMinutes);Assert.Equal(190,timer.MaxMinutes);Assert.Equal(key,timer.CatalogKey);Assert.Single(vm.Catalog.CompactFavorites);}finally{vm.Catalog.Dispose();vm.Spotlight.Dispose();}
 }
}
