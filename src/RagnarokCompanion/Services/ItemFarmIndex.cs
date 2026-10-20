using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed class ItemFarmIndex {
 static readonly Lazy<ItemFarmIndex> shared=new(()=>new(DatabasePath));public static ItemFarmIndex Shared=>shared.Value;
 public static string DatabasePath=>Path.Combine(AppContext.BaseDirectory,"Data","farmland-items.sqlite");
 readonly string? path;readonly FarmCatalog? memory;readonly ItemMonsterRules rules;
 public string SourceVersion=>"Farmland — dados públicos autorizados";
 public ItemFarmIndex(string database,ItemMonsterRules? rules=null){path=database;this.rules=rules??ItemMonsterRules.Load();}
 public ItemFarmIndex(FarmCatalog data):this(data,new ItemMonsterRules()){}
 public ItemFarmIndex(FarmCatalog data,ItemMonsterRules rules){memory=data;this.rules=rules;}
 SqliteConnection Open(){if(!File.Exists(path))throw new InvalidDataException("Banco de itens ausente. Importe os arquivos autorizados antes de distribuir.");var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();using var version=db.CreateCommand();version.CommandText="PRAGMA user_version";if(Convert.ToInt32(version.ExecuteScalar())!=2){db.Dispose();throw new InvalidDataException("Versão do banco de itens incompatível.");}return db;}
 public FarmItem? Find(int id){if(memory!=null)return memory.Items.FirstOrDefault(i=>i.Id==id);using var db=Open();using var command=db.CreateCommand();command.CommandText="SELECT id,name,name_en,unidentified_name FROM items WHERE id=$id";command.Parameters.AddWithValue("$id",id);using var reader=command.ExecuteReader();return reader.Read()?new(){Id=reader.GetInt32(0),Name=reader.GetString(1),EnglishName=reader.GetString(2),UnidentifiedName=reader.GetString(3)}:null;}
 public string Description(int id){if(memory!=null)return "";using var db=Open();using var command=db.CreateCommand();command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='item_descriptions'";if(Convert.ToInt32(command.ExecuteScalar())==0)return "";command.CommandText="SELECT raw_text FROM item_descriptions WHERE item_id=$id";command.Parameters.AddWithValue("$id",id);return command.ExecuteScalar() as string??"";}
 public List<FarmItem> Search(string query){var text=MvpSearchCoordinator.Normalize(query.Trim());if(text.Length==0)return new();if(int.TryParse(text,out int id))return Find(id) is {} item?new(){item}:new();
  if(memory!=null)return memory.Items.Where(i=>MvpSearchCoordinator.Normalize(i.Name).Contains(text,StringComparison.Ordinal)).Take(20).ToList();
  using var db=Open();using var command=db.CreateCommand();
  // Indexed prefix search first, then a bounded contains search for partial names.
  command.CommandText="SELECT id,name FROM items WHERE (search_name >= $text AND search_name < $end) OR (search_en >= $text AND search_en < $end) OR (search_unidentified >= $text AND search_unidentified < $end) ORDER BY search_name LIMIT 20";command.Parameters.AddWithValue("$text",text);command.Parameters.AddWithValue("$end",text+'\uffff');var result=new List<FarmItem>();using(var reader=command.ExecuteReader())while(reader.Read())result.Add(new(){Id=reader.GetInt32(0),Name=reader.GetString(1)});
  if(result.Count<20){command.CommandText="SELECT id,name FROM items WHERE (instr(search_name,$text)>0 OR instr(search_en,$text)>0 OR instr(search_unidentified,$text)>0) ORDER BY search_name LIMIT 40";using var reader=command.ExecuteReader();while(reader.Read()&&result.Count<20){id=reader.GetInt32(0);if(result.All(i=>i.Id!=id))result.Add(new(){Id=id,Name=reader.GetString(1)});}}
  return result;
 }
 public List<FarmResult> Rank(int itemId,double minimum,bool normalOnly,bool bestFarm){
  var candidates=new List<(FarmMonster Monster,double Chance)>();
  if(memory!=null){foreach(var group in memory.Drops.Where(d=>d.ItemId==itemId&&d.Chance>=minimum&&d.Chance is >0 and <=100).GroupBy(d=>d.MonsterId)){var monster=memory.Monsters.FirstOrDefault(m=>m.Id==group.Key);if(monster!=null)candidates.Add((monster,group.Max(d=>d.Chance)));}}
  else{using var db=Open();using var command=db.CreateCommand();command.CommandText="SELECT m.id,m.name,m.special,d.chance FROM drops d JOIN monsters m ON m.id=d.monster_id WHERE d.item_id=$item AND d.chance >= $minimum";command.Parameters.AddWithValue("$item",itemId);command.Parameters.AddWithValue("$minimum",minimum);
   using(var reader=command.ExecuteReader())while(reader.Read())candidates.Add((new(){Id=reader.GetInt32(0),Name=reader.GetString(1),IsSpecial=reader.GetBoolean(2)},reader.GetDouble(3)));
   command.Parameters.Clear();command.CommandText="SELECT map_code,map_name,population,source FROM spawns WHERE monster_id=$id";command.Parameters.Add("$id",SqliteType.Integer);
   foreach(var row in candidates){command.Parameters["$id"].Value=row.Monster.Id;using var reader=command.ExecuteReader();while(reader.Read())row.Monster.Spawns.Add(new(){Map=reader.GetString(0),Name=reader.GetString(1),Count=reader.IsDBNull(2)?null:reader.GetInt32(2),Evidence=reader.GetString(3)});}
  }
  var rows=candidates.Select(row=>{bool special=rules.Excluded(row.Monster.Id,row.Monster.Name,classified:row.Monster.IsSpecial);row.Monster.IsSpecial=special;var available=row.Monster.Spawns.Any(s=>s.Evidence=="Farmland")?row.Monster.Spawns.Where(s=>s.Evidence=="Farmland"):row.Monster.Spawns;var map=available.Where(s=>!string.IsNullOrWhiteSpace(s.Map)&&!s.Map.Contains('@')&&!string.IsNullOrWhiteSpace(s.Evidence)&&(s.Count==null||s.Count>0)).OrderByDescending(s=>s.Count??-1).ThenBy(s=>s.Map,StringComparer.Ordinal).FirstOrDefault();return ItemFarmMapResolver.ResolveLocal(new FarmResult{Monster=row.Monster,BestMap=map,Chance=row.Chance});}).Where(r=>!normalOnly||r.Farmable);
  return ItemFarmMapResolver.Order(rows,bestFarm);

 }
}
