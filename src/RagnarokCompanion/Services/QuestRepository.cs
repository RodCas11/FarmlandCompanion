using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using YamlDotNet.Serialization;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class QuestRepository {
 public sealed class Shop {public string Name {get;set;}="";public List<Entry> Items {get;set;}=new();}
 public sealed class Entry {public int? Index {get;set;}public string Item {get;set;}="";public long Zeny {get;set;}public List<Requirement> RequiredItems {get;set;}=new();}
 public sealed class Requirement {public string Item {get;set;}="";public long Amount {get;set;}public int Refine {get;set;}}
 public sealed class Equipment {public int Id {get;set;}public string AegisName {get;set;}="";public string Type {get;set;}="";public Dictionary<string,bool> Locations {get;set;}=new();}
 public static int Import(string source,string itemDirectory,string database){
  if(!Path.GetFileName(source).Equals("quests.txt",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Use exclusivamente quests.txt para as receitas.");
  string staged=database+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.Copy(database,staged);int count=ImportCore(source,itemDirectory,staged);File.Move(staged,database,true);return count;}
  finally{if(File.Exists(staged))File.Delete(staged);}
 }
 static int ImportCore(string source,string itemDirectory,string database){
  var previous=Load(database);var farmlandLua=Path.Combine(itemDirectory,"farmland.lua");if(File.Exists(farmlandLua))FarmlandLuaImporter.Import(farmlandLua,database);
  var additionalLua=Path.Combine(itemDirectory,"farmlandc.lua");if(File.Exists(additionalLua))FarmlandLuaImporter.Import(additionalLua,database,new[]{16074,28717,28728});
  var bro=Path.Combine(itemDirectory,"bRO.lua");if(File.Exists(bro))FarmlandLuaImporter.Import(bro,database,new[]{540127},serverSource:false);
  var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();var lookup=new Dictionary<string,int>(StringComparer.Ordinal);var equipment=new Dictionary<string,Equipment>(StringComparer.Ordinal);
  foreach(var name in new[]{"item_db_equip.yml","item_db_etc.yml","item_db_usable.yml"}){using var input=File.OpenText(Path.Combine(itemDirectory,name));foreach(var item in parser.Deserialize<FarmlandItemImporter.Document<Equipment>>(input).Body){lookup.Add(item.AegisName,item.Id);equipment.Add(item.AegisName,item);}}
  if(File.Exists(farmlandLua))foreach(var mapping in new Dictionary<string,int>{{"Gigant_Helm_BR",19220},{"Crow_Tengu_MaskF",19456},{"Crow_Tengu_MaskM",19457},{"Certificado_de_Caca",50036}})lookup[mapping.Key]=mapping.Value;
  if(File.Exists(additionalLua)){lookup["Meteor_Strike_BR"]=16074;lookup["Valkyrjas_Dagger"]=28728;lookup["Valkyrias_Dagger"]=28728;}
  if(File.Exists(bro))lookup["Livro_Muscular"]=540127;
  using var text=new StreamReader(source,System.Text.Encoding.UTF8,true);var shops=parser.Deserialize<List<Shop>>(text)??throw new InvalidDataException("Quests ausentes.");
  string[] categories={"Topo","Meio","Baixo","Escudos","Capas","Acessórios","Armas","Outros"};string[] suffixes={"Topo","Meio","Baixo","Escudo","Capa","Acessorio","Arma","Outros"};var quests=new List<QuestDefinition>();var index=new ItemFarmIndex(database);
  foreach(var shop in shops){bool normal=shop.Name=="Loja_Quest";if(!normal&&!shop.Name.StartsWith("Loja_Quest2_",StringComparison.Ordinal))throw new InvalidDataException("Grupo de quest desconhecido.");int category=Array.FindIndex(suffixes,s=>shop.Name.EndsWith("_"+s,StringComparison.Ordinal));
   foreach(var entry in shop.Items){int Resolve(string aegis)=>lookup.TryGetValue(aegis.Trim(),out int id)&&index.Find(id)!=null?id:0;
    string aegis=entry.Item.Trim();int reward=Resolve(aegis);if(entry.Zeny<0)throw new InvalidDataException("Custo inválido.");string kind=normal?"Normais":"Raras";string identified=Category(equipment.GetValueOrDefault(aegis)??equipment.Values.FirstOrDefault(e=>e.Id==reward));string group=identified!="Outros"?identified:normal?"Outros":categories[category<0?7:category];
    string sourceKey=shop.Name+":"+(entry.Index??shop.Items.IndexOf(entry));var old=previous.FirstOrDefault(q=>q.SourceKey==sourceKey&&q.AegisName==aegis)??previous.FirstOrDefault(q=>q.SourceKey.Length==0&&q.Kind==kind&&reward>0&&q.ItemId==reward&&q.Category==group);
    int id=old?.Id??((normal?10000000:20000000)+(normal?0:(category<0?7:category)*1000)+(entry.Index??shop.Items.IndexOf(entry)));
    var quest=new QuestDefinition{Id=id,ItemId=reward,AegisName=aegis,SourceKey=sourceKey,Kind=kind,Name=index.Find(reward)?.Name??("Não identificado: "+aegis),Category=group,Zeny=entry.Zeny};
    int nextIndex=old?.Materials.Select(m=>m.Index).DefaultIfEmpty(-1).Max()+1??0;var used=new HashSet<int>();
    foreach(var material in entry.RequiredItems){if(material.Amount<=0||material.Refine<0||material.Refine>20)throw new InvalidDataException("Requisito inválido.");int materialId=Resolve(material.Item);var prior=old?.Materials.FirstOrDefault(m=>!used.Contains(m.Index)&&m.ItemId>0&&m.ItemId==materialId&&m.Amount==material.Amount&&m.Refine==material.Refine);int position=prior?.Index??nextIndex++;used.Add(position);quest.Materials.Add(new(){Index=position,ItemId=materialId,AegisName=material.Item.Trim(),Name=index.Find(materialId)?.Name??(material.Item.Trim()=="Certificado_de_Caca"?"Certificado de Caça":"Não identificado: "+material.Item.Trim()),Amount=material.Amount,Refine=material.Refine});}quests.Add(quest);
   }
  }
  if(quests.Count(q=>q.Kind=="Normais")!=100||quests.Count(q=>q.Kind=="Raras")!=100||quests.Select(q=>q.Kind+"|"+q.AegisName+"|"+q.Zeny+"|"+string.Join(";",q.Materials.OrderBy(m=>m.AegisName).Select(m=>m.AegisName+":"+m.Amount+":"+m.Refine))).Distinct().Count()!=200||quests.Select(q=>q.Id).Distinct().Count()!=200)throw new InvalidDataException("Esperadas 100 quests normais e 100 raras, sem duplicatas.");
  Write(quests,database);return quests.Count;
 }
 public static string Category(Equipment? item){if(item==null)return "Outros";bool At(params string[] slots)=>slots.Any(s=>item.Locations.GetValueOrDefault(s));if(At("Head_Top","Costume_Head_Top"))return "Topo";if(At("Head_Mid","Costume_Head_Mid"))return "Meio";if(At("Head_Low","Costume_Head_Low"))return "Baixo";if(At("Garment","Costume_Garment"))return "Capas";if(At("Both_Accessory","Accessory_Left","Accessory_Right","Left_Accessory","Right_Accessory","Shadow_Left_Accessory","Shadow_Right_Accessory"))return "Acessórios";if(item.Type=="Weapon")return "Armas";if(At("Left_Hand"))return "Escudos";return "Outros";}
 public static void Preserve(string source,string destination){if(File.Exists(source)){var quests=Load(source);if(quests.Count>0)Write(quests,destination);}}
 static void Write(List<QuestDefinition> quests,string database){
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false}.ToString());db.Open();using var transaction=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=transaction;
  cmd.CommandText="DROP TABLE IF EXISTS quest_materials;DROP TABLE IF EXISTS quests;CREATE TABLE quests(id INTEGER PRIMARY KEY,item_id INTEGER NOT NULL,category TEXT NOT NULL,zeny INTEGER NOT NULL,name TEXT NOT NULL,kind TEXT NOT NULL,aegis_name TEXT NOT NULL,source_key TEXT NOT NULL UNIQUE); CREATE TABLE quest_materials(quest_id INTEGER NOT NULL,position INTEGER NOT NULL,item_id INTEGER NOT NULL,amount INTEGER NOT NULL,refine INTEGER NOT NULL,name TEXT NOT NULL,aegis_name TEXT NOT NULL,PRIMARY KEY(quest_id,position));";cmd.ExecuteNonQuery();
  foreach(var quest in quests){cmd.Parameters.Clear();cmd.CommandText="INSERT INTO quests VALUES($id,$item,$category,$zeny,$name,$kind,$aegis,$source)";cmd.Parameters.AddWithValue("$id",quest.Id);cmd.Parameters.AddWithValue("$item",quest.ItemId);cmd.Parameters.AddWithValue("$category",quest.Category);cmd.Parameters.AddWithValue("$zeny",quest.Zeny);cmd.Parameters.AddWithValue("$name",quest.Name);cmd.Parameters.AddWithValue("$kind",quest.Kind);cmd.Parameters.AddWithValue("$source",quest.SourceKey.Length>0?quest.SourceKey:"legacy:"+quest.Id);cmd.Parameters.AddWithValue("$aegis",quest.AegisName);cmd.ExecuteNonQuery();
   foreach(var material in quest.Materials){cmd.Parameters.Clear();cmd.CommandText="INSERT INTO quest_materials VALUES($quest,$position,$item,$amount,$refine,$name,$aegis)";cmd.Parameters.AddWithValue("$quest",quest.Id);cmd.Parameters.AddWithValue("$position",material.Index);cmd.Parameters.AddWithValue("$item",material.ItemId);cmd.Parameters.AddWithValue("$amount",material.Amount);cmd.Parameters.AddWithValue("$refine",material.Refine);cmd.Parameters.AddWithValue("$name",material.Name);cmd.Parameters.AddWithValue("$aegis",material.AegisName);cmd.ExecuteNonQuery();}}
  transaction.Commit();
 }
 public static List<QuestDefinition> Load(string? database=null){
  database??=ItemFarmIndex.DatabasePath;if(!File.Exists(database))return new();using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name='quests'";if(Convert.ToInt32(cmd.ExecuteScalar())==0)return new();
  cmd.CommandText="SELECT COUNT(*) FROM pragma_table_info('quests') WHERE name='kind'";bool modern=Convert.ToInt32(cmd.ExecuteScalar())>0;
  cmd.CommandText="SELECT q.id,q.item_id,COALESCE(i.name,q.name),q.category,q.zeny,"+(modern?"q.kind,q.aegis_name,q.source_key":"'Raras',''")+" FROM quests q LEFT JOIN items i ON i.id=q.item_id ORDER BY q.name";var quests=new List<QuestDefinition>();using(var reader=cmd.ExecuteReader())while(reader.Read())quests.Add(new(){Id=reader.GetInt32(0),ItemId=reader.GetInt32(1),Name=reader.GetString(2),Category=reader.GetString(3),Zeny=reader.GetInt64(4),Kind=reader.GetString(5),AegisName=reader.GetString(6),SourceKey=modern?reader.GetString(7):""});
  cmd.CommandText="SELECT m.quest_id,m.position,m.item_id,COALESCE(i.name,m.name),m.amount,m.refine,"+(modern?"m.aegis_name":"''")+" FROM quest_materials m LEFT JOIN items i ON i.id=m.item_id ORDER BY m.quest_id,m.position";using(var reader=cmd.ExecuteReader())while(reader.Read())quests.First(q=>q.Id==reader.GetInt32(0)).Materials.Add(new(){Index=reader.GetInt32(1),ItemId=reader.GetInt32(2),Name=reader.GetString(3),Amount=reader.GetInt64(4),Refine=reader.GetInt32(5),AegisName=reader.GetString(6)});return quests;
 }
}
