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
 public sealed class Entry {public string Item {get;set;}="";public long Zeny {get;set;}public List<Requirement> RequiredItems {get;set;}=new();}
 public sealed class Requirement {public string Item {get;set;}="";public long Amount {get;set;}public int Refine {get;set;}}
 public static int Import(string source,string itemDirectory,string database){
  string staged=database+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.Copy(database,staged);int count=ImportCore(source,itemDirectory,staged);File.Move(staged,database,true);return count;}
  finally{if(File.Exists(staged))File.Delete(staged);}
 }
 static int ImportCore(string source,string itemDirectory,string database){
  var farmlandLua=Path.Combine(itemDirectory,"farmland.lua");
  if(File.Exists(farmlandLua))FarmlandLuaImporter.Import(farmlandLua,database);
  var additionalLua=Path.Combine(itemDirectory,"farmlandc.lua");
  if(File.Exists(additionalLua))FarmlandLuaImporter.Import(additionalLua,database,new[]{16074,28717,28728});
  var bro=Path.Combine(itemDirectory,"bRO.lua");
  if(File.Exists(bro))FarmlandLuaImporter.Import(bro,database,new[]{540127},serverSource:false);
  var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();var lookup=new Dictionary<string,int>(StringComparer.Ordinal);
  foreach(var name in new[]{"item_db_equip.yml","item_db_etc.yml","item_db_usable.yml"}){using var input=File.OpenText(Path.Combine(itemDirectory,name));foreach(var item in parser.Deserialize<FarmlandItemImporter.Document<FarmlandItemImporter.Item>>(input).Body)lookup.Add(item.AegisName,item.Id);}
  // Explicit correspondences confirmed by the user against the server iteminfo.
  if(File.Exists(farmlandLua))foreach(var mapping in new Dictionary<string,int>{{"Gigant_Helm_BR",19220},{"Crow_Tengu_MaskF",19456},{"Crow_Tengu_MaskM",19457}})lookup[mapping.Key]=mapping.Value;
  if(File.Exists(additionalLua)){lookup["Meteor_Strike_BR"]=16074;lookup["Valkyrjas_Dagger"]=28728;lookup["Valkyrias_Dagger"]=28728;}
  if(File.Exists(bro))lookup["Livro_Muscular"]=540127;
  using var text=new StreamReader(source,System.Text.Encoding.UTF8,true);var shops=parser.Deserialize<List<Shop>>(text)??throw new InvalidDataException("Quests ausentes.");
  string[] categories={"Topo","Meio","Baixo","Escudos","Capas","Acessórios","Armas"};string[] suffixes={"Topo","Meio","Baixo","Escudo","Capa","Acessorio","Arma"};var quests=new List<QuestDefinition>();var index=new ItemFarmIndex(database);
  foreach(var shop in shops){int category=Array.FindIndex(suffixes,s=>shop.Name.EndsWith("_"+s,StringComparison.Ordinal));if(category<0)throw new InvalidDataException("Categoria de quest desconhecida.");
   foreach(var entry in shop.Items){int Resolve(string aegis)=>lookup.TryGetValue(aegis.Trim(),out int id)&&index.Find(id)!=null?id:0;
    int reward=Resolve(entry.Item);if(entry.Zeny<0)throw new InvalidDataException("Custo inválido.");var quest=new QuestDefinition{Id=category*1000+shop.Items.IndexOf(entry)+1,ItemId=reward,Name=index.Find(reward)?.Name??((entry.Item.Trim()=="Valkyrjas_Dagger"||entry.Item.Trim()=="Valkyrias_Dagger")?"Adaga das Valquírias":"Não identificado: "+entry.Item.Trim()),Category=categories[category],Zeny=entry.Zeny};
    foreach(var material in entry.RequiredItems.Where(r=>r.Item.Trim()!="Certificado_de_Caca")){if(material.Amount<=0||material.Refine<0||material.Refine>20)throw new InvalidDataException("Requisito inválido.");int id=Resolve(material.Item);quest.Materials.Add(new(){Index=quest.Materials.Count,ItemId=id,Name=index.Find(id)?.Name??("Não identificado: "+material.Item.Trim()),Amount=material.Amount,Refine=material.Refine});}quests.Add(quest);
   }
  }
  if(quests.Count!=99||quests.Select(q=>q.Id).Distinct().Count()!=99)throw new InvalidDataException("Esperadas 99 recompensas distintas.");
  Write(quests,database);return quests.Count;
 }
 public static void Preserve(string source,string destination){if(File.Exists(source)){var quests=Load(source);if(quests.Count>0)Write(quests,destination);}}
 static void Write(List<QuestDefinition> quests,string database){
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false}.ToString());db.Open();using var transaction=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=transaction;
  cmd.CommandText="CREATE TABLE IF NOT EXISTS quests(id INTEGER PRIMARY KEY,item_id INTEGER NOT NULL,category TEXT NOT NULL,zeny INTEGER NOT NULL,name TEXT NOT NULL); CREATE TABLE IF NOT EXISTS quest_materials(quest_id INTEGER NOT NULL,position INTEGER NOT NULL,item_id INTEGER NOT NULL,amount INTEGER NOT NULL,refine INTEGER NOT NULL,name TEXT NOT NULL,PRIMARY KEY(quest_id,position)); DELETE FROM quest_materials; DELETE FROM quests;";cmd.ExecuteNonQuery();
  foreach(var quest in quests){cmd.Parameters.Clear();cmd.CommandText="INSERT INTO quests VALUES($id,$item,$category,$zeny,$name)";cmd.Parameters.AddWithValue("$id",quest.Id);cmd.Parameters.AddWithValue("$item",quest.ItemId);cmd.Parameters.AddWithValue("$category",quest.Category);cmd.Parameters.AddWithValue("$zeny",quest.Zeny);cmd.Parameters.AddWithValue("$name",quest.Name);cmd.ExecuteNonQuery();
   foreach(var material in quest.Materials){cmd.Parameters.Clear();cmd.CommandText="INSERT INTO quest_materials VALUES($quest,$position,$item,$amount,$refine,$name)";cmd.Parameters.AddWithValue("$quest",quest.Id);cmd.Parameters.AddWithValue("$position",material.Index);cmd.Parameters.AddWithValue("$item",material.ItemId);cmd.Parameters.AddWithValue("$amount",material.Amount);cmd.Parameters.AddWithValue("$refine",material.Refine);cmd.Parameters.AddWithValue("$name",material.Name);cmd.ExecuteNonQuery();}}
  transaction.Commit();
 }
 public static List<QuestDefinition> Load(string? database=null){
  database??=ItemFarmIndex.DatabasePath;if(!File.Exists(database))return new();using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name='quests'";if(Convert.ToInt32(cmd.ExecuteScalar())==0)return new();
  cmd.CommandText="SELECT q.id,q.item_id,COALESCE(i.name,q.name),q.category,q.zeny FROM quests q LEFT JOIN items i ON i.id=q.item_id ORDER BY q.name";var quests=new List<QuestDefinition>();using(var reader=cmd.ExecuteReader())while(reader.Read())quests.Add(new(){Id=reader.GetInt32(0),ItemId=reader.GetInt32(1),Name=reader.GetString(2),Category=reader.GetString(3),Zeny=reader.GetInt64(4)});
  cmd.CommandText="SELECT m.quest_id,m.position,m.item_id,COALESCE(i.name,m.name),m.amount,m.refine FROM quest_materials m LEFT JOIN items i ON i.id=m.item_id ORDER BY m.quest_id,m.position";using(var reader=cmd.ExecuteReader())while(reader.Read())quests.First(q=>q.Id==reader.GetInt32(0)).Materials.Add(new(){Index=reader.GetInt32(1),ItemId=reader.GetInt32(2),Name=reader.GetString(3),Amount=reader.GetInt64(4),Refine=reader.GetInt32(5)});return quests;
 }
}
