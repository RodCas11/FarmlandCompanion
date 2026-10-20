using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using YamlDotNet.Serialization;
namespace RagnarokCompanion.Services;
// Only explicitly selected public scalars enter SQLite. Never serialize YAML nodes or errors.
public static class FarmlandItemImporter {
 public sealed class Document<T> {public List<T> Body {get;set;}=new();}
 public sealed class Item {public int Id {get;set;}public string AegisName {get;set;}="";public string Name {get;set;}="";}
 public sealed class Monster {public int Id {get;set;}public string AegisName {get;set;}="";public string Name {get;set;}="";public long MvpExp {get;set;} public string Class {get;set;}=""; public List<Drop> Drops {get;set;}=new();}
 public sealed class Drop {public string Item {get;set;}="";public int Rate {get;set;}}
 public static string Import(string sourceDirectory,string destination,string? rulesPath=null){
  destination=Path.GetFullPath(destination);var root=Path.GetFullPath(sourceDirectory);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
  var rules=ItemMonsterRules.Load(rulesPath);var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();var items=new Dictionary<int,Item>();var aegis=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
  // YAML parsing is restricted to DTO fields; all scripts/settings are ignored and never stored.
  foreach(var file in new[]{"item_db_equip.yml","item_db_etc.yml","item_db_usable.yml"}){
   using var input=File.OpenText(Path.Combine(root,file));var document=parser.Deserialize<Document<Item>>(input)??throw new InvalidDataException("Banco de itens inválido.");
   foreach(var item in document.Body){Validate(item.Id,item.Name);if(string.IsNullOrWhiteSpace(item.AegisName)||items.ContainsKey(item.Id)||aegis.ContainsKey(item.AegisName))throw new InvalidDataException("Identificadores de itens vazios ou duplicados.");items.Add(item.Id,item);aegis.Add(item.AegisName,item.Id);}
  }
  List<Monster> monsters;using(var input=File.OpenText(Path.Combine(root,"mob_db.yml")))monsters=(parser.Deserialize<Document<Monster>>(input)??throw new InvalidDataException("Banco de monstros inválido.")).Body;
  string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";int dropCount=0;
  try{
   using(var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temporary,Pooling=false,ForeignKeys=true}.ToString())){
    db.Open();using(var schema=db.CreateCommand()){schema.CommandText="""
     PRAGMA journal_mode=DELETE;
     PRAGMA user_version=1;
     CREATE TABLE items(id INTEGER PRIMARY KEY,name TEXT NOT NULL,search_name TEXT NOT NULL);
     CREATE INDEX ix_items_name ON items(search_name);
     CREATE TABLE monsters(id INTEGER PRIMARY KEY,name TEXT NOT NULL,special INTEGER NOT NULL CHECK(special IN (0,1)));
     CREATE INDEX ix_monsters_name ON monsters(name COLLATE NOCASE);
     CREATE TABLE drops(item_id INTEGER NOT NULL REFERENCES items(id),monster_id INTEGER NOT NULL REFERENCES monsters(id),chance REAL NOT NULL CHECK(chance>0 AND chance<=100),PRIMARY KEY(item_id,monster_id));
     CREATE INDEX ix_drops_monster ON drops(monster_id,item_id);
     CREATE TABLE spawns(monster_id INTEGER NOT NULL REFERENCES monsters(id),map_code TEXT NOT NULL,map_name TEXT NOT NULL,population INTEGER CHECK(population>0),PRIMARY KEY(monster_id,map_code));
     """;schema.ExecuteNonQuery();}
    using var transaction=db.BeginTransaction();using var insert=db.CreateCommand();insert.Transaction=transaction;
    insert.CommandText="INSERT INTO items VALUES($id,$name,$search)";insert.Parameters.Add("$id",SqliteType.Integer);insert.Parameters.Add("$name",SqliteType.Text);insert.Parameters.Add("$search",SqliteType.Text);
    foreach(var item in items.Values){insert.Parameters["$id"].Value=item.Id;insert.Parameters["$name"].Value=item.Name;insert.Parameters["$search"].Value=MvpSearchCoordinator.Normalize(item.Name);insert.ExecuteNonQuery();}
    using var mob=db.CreateCommand();mob.Transaction=transaction;mob.CommandText="INSERT INTO monsters VALUES($id,$name,$special)";mob.Parameters.Add("$id",SqliteType.Integer);mob.Parameters.Add("$name",SqliteType.Text);mob.Parameters.Add("$special",SqliteType.Integer);
    using var drop=db.CreateCommand();drop.Transaction=transaction;drop.CommandText="INSERT INTO drops VALUES($item,$monster,$chance) ON CONFLICT(item_id,monster_id) DO UPDATE SET chance=MAX(chance,excluded.chance)";drop.Parameters.Add("$item",SqliteType.Integer);drop.Parameters.Add("$monster",SqliteType.Integer);drop.Parameters.Add("$chance",SqliteType.Real);
    foreach(var monster in monsters){Validate(monster.Id,monster.Name);mob.Parameters["$id"].Value=monster.Id;mob.Parameters["$name"].Value=monster.Name;mob.Parameters["$special"].Value=rules.Excluded(monster.Id,monster.Name,monster.AegisName,monster.MvpExp>0||monster.Class is "Guardian" or "Battlefield" or "Event")?1:0;mob.ExecuteNonQuery();
     foreach(var value in monster.Drops??new()){if(value.Rate<0||value.Rate>10000)throw new InvalidDataException("Taxa fora do intervalo 0–10000.");if(value.Rate==0)continue;if(!aegis.TryGetValue(value.Item,out int id))throw new InvalidDataException("Drop referencia um item ausente nos arquivos fornecidos.");drop.Parameters["$item"].Value=id;drop.Parameters["$monster"].Value=monster.Id;drop.Parameters["$chance"].Value=value.Rate/100.0;drop.ExecuteNonQuery();dropCount++;}
    }
    transaction.Commit();using var check=db.CreateCommand();check.CommandText="PRAGMA integrity_check";if((string?)check.ExecuteScalar()!="ok")throw new InvalidDataException("Falha na integridade do SQLite.");
   }
   // Same-directory atomic replacement. A failure never removes the last usable database.
   var lua=Path.Combine(root,"bRO.lua"); ItemFarmDataUpdater.Update(temporary,File.Exists(lua)?lua:null,File.Exists(destination)?destination:null);
   var questSource=Path.Combine(root,"quests.txt");if(File.Exists(questSource))QuestRepository.Import(questSource,root,temporary);else QuestRepository.Preserve(destination,temporary);
   File.Move(temporary,destination,true);
   return $"Importação concluída: {items.Count} itens, {monsters.Count} monstros, {dropCount} relações de drop. Nenhum spawn informado.";
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 static void Validate(int id,string name){if(id<=0||string.IsNullOrWhiteSpace(name)||name.Length>250)throw new InvalidDataException("ID ou nome público inválido.");}
 // Future spawn imports must supply only public, confirmed permanent populations.
 public sealed class PublicSpawn {public int MonsterId {get;set;}public string MapCode {get;set;}="";public string MapName {get;set;}="";public int? Count {get;set;}public bool Regular {get;set;}}
 public static void ImportSpawns(string database,IEnumerable<PublicSpawn> records){
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false,ForeignKeys=true}.ToString());db.Open();using var transaction=db.BeginTransaction();using var command=db.CreateCommand();command.Transaction=transaction;command.CommandText="INSERT INTO spawns(monster_id,map_code,map_name,population) VALUES($monster,$map,$name,$count) ON CONFLICT(monster_id,map_code) DO UPDATE SET map_name=excluded.map_name,population=excluded.population";command.Parameters.Add("$monster",SqliteType.Integer);command.Parameters.Add("$map",SqliteType.Text);command.Parameters.Add("$name",SqliteType.Text);command.Parameters.Add("$count",SqliteType.Integer);
  var groups=records.ToList();foreach(var record in groups){if(!record.Regular||record.MonsterId<=0||!System.Text.RegularExpressions.Regex.IsMatch(record.MapCode,"^[a-z0-9_]+$")||record.Count is <=0||record.MapName.Length>250)throw new InvalidDataException("Spawn sem confirmação regular ou com dados inválidos.");}
  foreach(var group in groups.GroupBy(r=>(r.MonsterId,r.MapCode))){var record=group.First();command.Parameters["$monster"].Value=record.MonsterId;command.Parameters["$map"].Value=record.MapCode;command.Parameters["$name"].Value=string.IsNullOrWhiteSpace(record.MapName)?record.MapCode:record.MapName;command.Parameters["$count"].Value=group.Any(r=>r.Count==null)?DBNull.Value:group.Sum(r=>(long)r.Count!.Value);command.ExecuteNonQuery();}transaction.Commit();
 }
}
