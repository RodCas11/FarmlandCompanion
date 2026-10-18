using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
namespace RagnarokCompanion.Services;
public static class ItemFarmDataUpdater {
 public static string Update(string database,string? lua=null,string? previousDatabase=null){
  var names=lua!=null?LuaItemNameReader.Read(lua):null;
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false}.ToString());db.Open();using var transaction=db.BeginTransaction();using var command=db.CreateCommand();command.Transaction=transaction;command.CommandText="PRAGMA user_version";int version=Convert.ToInt32(command.ExecuteScalar());
  if(version==1){command.CommandText="""
   ALTER TABLE items ADD COLUMN name_en TEXT NOT NULL DEFAULT '';
   ALTER TABLE items ADD COLUMN unidentified_name TEXT NOT NULL DEFAULT '';
   ALTER TABLE items ADD COLUMN search_en TEXT NOT NULL DEFAULT '';
   ALTER TABLE items ADD COLUMN search_unidentified TEXT NOT NULL DEFAULT '';
   UPDATE items SET name_en=name,search_en=search_name;
   CREATE INDEX ix_items_english ON items(search_en);
   CREATE INDEX ix_items_unidentified ON items(search_unidentified);
   ALTER TABLE spawns ADD COLUMN source TEXT NOT NULL DEFAULT 'Farmland';
   PRAGMA user_version=2;
   """;command.ExecuteNonQuery();}else if(version!=2)throw new InvalidDataException("Versão do banco incompatível.");
  if(previousDatabase!=null&&File.Exists(previousDatabase)){
   command.CommandText="ATTACH DATABASE $old AS previous";command.Parameters.AddWithValue("$old",previousDatabase);command.ExecuteNonQuery();command.Parameters.Clear();command.CommandText="PRAGMA previous.user_version";int oldVersion=Convert.ToInt32(command.ExecuteScalar());
   if(oldVersion is 1 or 2){string source=oldVersion==2?"p.source":"'Farmland'";command.CommandText="INSERT INTO spawns(monster_id,map_code,map_name,population,source) SELECT p.monster_id,p.map_code,p.map_name,p.population,"+source+" FROM previous.spawns p JOIN monsters m ON m.id=p.monster_id WHERE "+source+"='Farmland' ON CONFLICT(monster_id,map_code) DO NOTHING";command.ExecuteNonQuery();}
   if(oldVersion==2&&names==null){command.CommandText="UPDATE items SET (name,search_name,unidentified_name,search_unidentified)=(SELECT p.name,p.search_name,p.unidentified_name,p.search_unidentified FROM previous.items p WHERE p.id=items.id) WHERE EXISTS(SELECT 1 FROM previous.items p WHERE p.id=items.id)";command.ExecuteNonQuery();}
  }
  int count=0;if(names!=null){command.CommandText="UPDATE items SET name=$name,search_name=$search,unidentified_name=$unknown,search_unidentified=$searchUnknown WHERE id=$id";command.Parameters.Add("$name",SqliteType.Text);command.Parameters.Add("$search",SqliteType.Text);command.Parameters.Add("$unknown",SqliteType.Text);command.Parameters.Add("$searchUnknown",SqliteType.Text);command.Parameters.Add("$id",SqliteType.Integer);
   foreach(var pair in names){command.Parameters["$id"].Value=pair.Key;command.Parameters["$name"].Value=pair.Value.Identified;command.Parameters["$search"].Value=MvpSearchCoordinator.Normalize(pair.Value.Identified);command.Parameters["$unknown"].Value=pair.Value.Unidentified;command.Parameters["$searchUnknown"].Value=MvpSearchCoordinator.Normalize(pair.Value.Unidentified);count+=command.ExecuteNonQuery();}}
  command.Parameters.Clear();command.CommandText="CREATE TABLE IF NOT EXISTS item_descriptions(item_id INTEGER PRIMARY KEY REFERENCES items(id),raw_text TEXT NOT NULL,source TEXT NOT NULL)";command.ExecuteNonQuery();
  if(names!=null){command.CommandText="INSERT INTO item_descriptions(item_id,raw_text,source) SELECT $id,$raw,'bRO.lua' WHERE EXISTS(SELECT 1 FROM items WHERE id=$id) ON CONFLICT(item_id) DO UPDATE SET raw_text=excluded.raw_text,source=excluded.source";command.Parameters.Add("$id",SqliteType.Integer);command.Parameters.Add("$raw",SqliteType.Text);foreach(var pair in names.Where(p=>!string.IsNullOrWhiteSpace(p.Value.Description))){command.Parameters["$id"].Value=pair.Key;command.Parameters["$raw"].Value=pair.Value.Description;command.ExecuteNonQuery();}}
  command.Parameters.Clear();command.CommandText="INSERT INTO spawns(monster_id,map_code,map_name,population,source) SELECT $id,$map,$name,$count,$source WHERE EXISTS(SELECT 1 FROM monsters WHERE id=$id) ON CONFLICT(monster_id,map_code) DO NOTHING";foreach(var name in new[]{"$id","$map","$name","$count","$source"})command.Parameters.AddWithValue(name,"");
  int maps=0;foreach(var record in PublicRegularSpawns.Shared.Records){command.Parameters["$id"].Value=record.MonsterId;command.Parameters["$map"].Value=record.MapCode;command.Parameters["$name"].Value=record.MapName;command.Parameters["$count"].Value=record.Count;command.Parameters["$source"].Value="rAthena Renewal — referência pública, não confirmado no Farmland. "+record.SourceUrl;maps+=command.ExecuteNonQuery();}
  transaction.Commit();return $"Nomes portugueses atualizados: {count}; referências públicas de mapas adicionadas: {maps}. Drops e spawns anteriores preservados.";
 }
}
