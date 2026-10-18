using Microsoft.Data.Sqlite;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class QuestRotationTests {
 [Fact] public void RotationCountsSaoPauloBoundariesIncludingDaysClosed(){
  var reference=new DateTimeOffset(2026,10,18,19,59,59,TimeSpan.FromHours(-3));
  Assert.Equal(0,SpotlightRotationService.Steps(reference,reference));Assert.Equal(1,SpotlightRotationService.Steps(reference,reference.AddSeconds(1)));Assert.Equal(4,SpotlightRotationService.Steps(reference,reference.AddDays(3).AddSeconds(1)));Assert.Equal(7,SpotlightRotationService.Steps(reference.AddSeconds(1),reference.AddDays(7).AddSeconds(1)));
 }
 [Fact] public void ConfiguredSequencePersistsAndCorrectionKeepsOrder(){
  Exception? error=null;var thread=new System.Threading.Thread(()=>{try{
   var store=new PersistenceService(Path.Combine(Path.GetTempPath(),"Farmland-rotation-"+Guid.NewGuid()));var time=new DateTimeOffset(2026,10,18,19,59,59,TimeSpan.FromHours(-3));
   store.Save("spotlight-catalog.json",SpotlightExtractedCatalog.LoadBundled());using var vm=new SpotlightViewModel(store,clock:()=>time);Assert.True(vm.HasRotation);Assert.Equal(SpotlightRotationService.OfficialOrder,store.Load<SpotlightSelection>("spotlight-selection.json").RotationOrder);vm.RotationDraft="Celestial → Infernal → Astral → Solar → Lunar → Estelar → Radiante";vm.SaveRotationCommand.Execute(null);vm.SelectedGroup=vm.Groups.Single(g=>g.Name=="Spotlight Celestial");vm.Confirm();
   time=time.AddSeconds(1);Assert.Equal("Spotlight Infernal",vm.ConfirmedGroup!.Name);Assert.False(vm.Pending);time=time.AddDays(2);Assert.Equal("Spotlight Solar",vm.ConfirmedGroup!.Name);
   using var reload=new SpotlightViewModel(store,clock:()=>time);Assert.Equal(vm.ConfirmedGroup.Name,reload.ConfirmedGroup!.Name);reload.SelectedGroup=reload.Groups.Single(g=>g.Name=="Spotlight Astral");reload.Confirm();Assert.Contains("Confirmado por você",reload.Status);time=time.AddDays(1);Assert.Equal("Spotlight Solar",reload.ConfirmedGroup!.Name);
   reload.RotationDraft="Celestial → Infernal";reload.SaveRotationCommand.Execute(null);Assert.True(reload.HasRotation);
  }catch(Exception ex){error=ex;}});thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
 }
 [Fact] public void ImportPreservesNinetyNineCostsRefinesAndExcludesCertificate(){
  string root=Path.Combine(Path.GetTempPath(),"Farmland-quests-"+Guid.NewGuid());Directory.CreateDirectory(root);string database=Path.Combine(root,"items.sqlite");
  try{
   using(var db=new SqliteConnection("Data Source="+database+";Pooling=False")){db.Open();using var command=db.CreateCommand();command.CommandText="PRAGMA user_version=2; CREATE TABLE items(id INTEGER PRIMARY KEY,name TEXT,name_en TEXT,unidentified_name TEXT);";command.ExecuteNonQuery();for(int id=1;id<=99;id++){command.CommandText=$"INSERT INTO items VALUES({id},'Equipamento {id}','Equipment {id}','')";command.ExecuteNonQuery();}}
   string body="Body:\n"+string.Join("\n",Enumerable.Range(1,99).Select(id=>$"  - Id: {id}\n    AegisName: Item_{id}\n    Name: Equipment {id}"));File.WriteAllText(Path.Combine(root,"item_db_equip.yml"),body);foreach(var name in new[]{"item_db_etc.yml","item_db_usable.yml"})File.WriteAllText(Path.Combine(root,name),"Body: []");
   string source=Path.Combine(root,"message.txt");File.WriteAllText(source,"- Name: Loja_Quest2_Topo\n  Items:\n"+string.Join("\n",Enumerable.Range(1,99).Select(id=>$"    - Item: Item_{id}\n      Zeny: {id*400000L}\n      RequiredItems:\n        - Item: Item_1\n          Amount: 300\n          Refine: 12\n        - Item: Certificado_de_Caca\n          Amount: 999")));
   Assert.Equal(99,QuestRepository.Import(source,root,database));var quests=QuestRepository.Load(database);Assert.Equal(99,quests.Count);Assert.All(quests,q=>{Assert.Single(q.Materials);Assert.Equal(300,q.Materials[0].Amount);Assert.Equal(12,q.Materials[0].Refine);Assert.Equal(q.ItemId*400000L,q.Zeny);});
   var store=new PersistenceService(Path.Combine(root,"state"));store.Save("quest-progress.json",new QuestProgress{Favorites=new(){quests[0].Id},Obtained=new(){quests[0].Id+":0"}});var saved=store.Load<QuestProgress>("quest-progress.json");Assert.Contains(quests[0].Id,saved.Favorites);Assert.Contains(quests[0].Id+":0",saved.Obtained);
   File.WriteAllText(source,"- Name: Loja_Quest2_Topo\n  Items: []");Assert.Throws<InvalidDataException>(()=>QuestRepository.Import(source,root,database));Assert.Equal(99,QuestRepository.Load(database).Count);
  }finally{SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
 }
}
