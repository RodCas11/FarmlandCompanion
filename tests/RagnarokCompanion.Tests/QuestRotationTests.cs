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
   time=time.AddSeconds(1);vm.Tick();Assert.Equal("Spotlight Infernal",vm.ConfirmedGroup!.Name);Assert.Equal(vm.ConfirmedGroup,vm.SelectedGroup);Assert.False(vm.Pending);time=time.AddDays(1);vm.Tick();Assert.Equal("Spotlight Astral",vm.SelectedGroup!.Name);Assert.All(vm.Rows,r=>Assert.Equal("astral",r.Map.GroupId));time=time.AddDays(1);vm.Tick();Assert.Equal("Spotlight Solar",vm.ConfirmedGroup!.Name);vm.SelectedGroup=vm.Groups.Single(g=>g.Id=="infernal");vm.Tick();Assert.Equal("infernal",vm.SelectedGroup.Id);
   using var reload=new SpotlightViewModel(store,clock:()=>time);Assert.Equal(vm.ConfirmedGroup.Name,reload.ConfirmedGroup!.Name);Assert.Equal(reload.ConfirmedGroup,reload.SelectedGroup);reload.SelectedGroup=reload.Groups.Single(g=>g.Name=="Spotlight Astral");reload.Confirm();Assert.Contains("Confirmado por você",reload.Status);time=time.AddDays(1);Assert.Equal("Spotlight Solar",reload.ConfirmedGroup!.Name);
   reload.RotationDraft="Celestial → Infernal";reload.SaveRotationCommand.Execute(null);Assert.True(reload.HasRotation);
  }catch(Exception ex){error=ex;}});thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
 }
 [Fact] public void TwoHundredRecipesKeepCertificatesRefinesAndLegacyProgress(){
  Assert.Equal("Acessórios",QuestRepository.Category(new QuestRepository.Equipment{Type="Armor",Locations=new(){{"Both_Accessory",true}}}));
  string root=Path.Combine(Path.GetTempPath(),"Farmland-quests-"+Guid.NewGuid());Directory.CreateDirectory(root);string database=Path.Combine(root,"items.sqlite");
  try{
   using(var db=new SqliteConnection("Data Source="+database+";Pooling=False")){db.Open();using var command=db.CreateCommand();command.CommandText="PRAGMA user_version=2; CREATE TABLE items(id INTEGER PRIMARY KEY,name TEXT,name_en TEXT,unidentified_name TEXT); CREATE TABLE quests(id INTEGER PRIMARY KEY,item_id INTEGER,category TEXT,zeny INTEGER,name TEXT);CREATE TABLE quest_materials(quest_id INTEGER,position INTEGER,item_id INTEGER,amount INTEGER,refine INTEGER,name TEXT);INSERT INTO quests VALUES(13,101,'Topo',400000,'Equipamento 101');INSERT INTO quest_materials VALUES(13,0,1,300,12,'Equipamento 1');";command.ExecuteNonQuery();for(int id=1;id<=200;id++){command.CommandText=$"INSERT INTO items VALUES({id},'Equipamento {id}','Equipment {id}','')";command.ExecuteNonQuery();}}
   string body="Body:\n"+string.Join("\n",Enumerable.Range(1,200).Select(id=>$"  - Id: {id}\n    AegisName: Item_{id}\n    Name: Equipment {id}"));File.WriteAllText(Path.Combine(root,"item_db_equip.yml"),body);foreach(var name in new[]{"item_db_etc.yml","item_db_usable.yml"})File.WriteAllText(Path.Combine(root,name),"Body: []");
   string Entries(int start)=>string.Join("\n",Enumerable.Range(start,100).Select(id=>$"    - Item: Item_{id}\n      Zeny: {id*400000L}\n      RequiredItems:\n        - Item: Item_1\n          Amount: 300\n          Refine: 12\n        - Item: Certificado_de_Caca\n          Amount: 999"));string source=Path.Combine(root,"quests.txt");File.WriteAllText(source,"- Name: Loja_Quest\n  Items:\n"+Entries(1)+"\n- Name: Loja_Quest2_Topo\n  Items:\n"+Entries(101));
   var store=new PersistenceService(Path.Combine(root,"state"));store.Save("quest-progress.json",new QuestProgress{Favorites=new(){13},Obtained=new(){"13:0"}});
   Assert.Equal(200,QuestRepository.Import(source,root,database));var quests=QuestRepository.Load(database);Assert.Equal(100,quests.Count(q=>q.Kind=="Normais"));Assert.Equal(100,quests.Count(q=>q.Kind=="Raras"));Assert.All(quests,q=>{Assert.Equal(2,q.Materials.Count);Assert.Equal(300,q.Materials[0].Amount);Assert.Equal(12,q.Materials[0].Refine);Assert.Equal(999,q.Materials[1].Amount);Assert.Equal("Certificado_de_Caca",q.Materials[1].AegisName);Assert.Equal(q.ItemId*400000L,q.Zeny);});Assert.Equal(13,quests.Single(q=>q.Kind=="Raras"&&q.ItemId==101).Id);
   Assert.Equal(200,QuestRepository.Import(source,root,database));Assert.Equal(quests.Select(q=>q.Id),QuestRepository.Load(database).Select(q=>q.Id));var saved=store.Load<QuestProgress>("quest-progress.json");Assert.Contains(13,saved.Favorites);Assert.Contains("13:0",saved.Obtained);
   Assert.Throws<InvalidDataException>(()=>QuestRepository.Import(Path.Combine(root,"message.txt"),root,database));File.WriteAllText(source,"- Name: Loja_Quest2_Topo\n  Items: []");Assert.Throws<InvalidDataException>(()=>QuestRepository.Import(source,root,database));Assert.Equal(200,QuestRepository.Load(database).Count);
  }finally{SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
 }
}
