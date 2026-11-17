using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using YamlDotNet.Serialization;
namespace RagnarokCompanion.Services;
public static class CompleteItemCatalogImporter
{
 public static string Import(string source,string database,string equipment){
  var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();var items=new Dictionary<int,FarmlandItemImporter.Item>();
  foreach(var file in new[]{"item_db_equip.yml","item_db_etc.yml","item_db_usable.yml"}){using var reader=File.OpenText(Path.Combine(source,file));foreach(var item in parser.Deserialize<FarmlandItemImporter.Document<FarmlandItemImporter.Item>>(reader).Body)items[item.Id]=item;}
  string temp=database+".complete.tmp",meta=equipment+".complete.tmp";File.Copy(database,temp,true);
  try{
   using(var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temp,Pooling=false}.ToString())){db.Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT INTO items(id,name,search_name,name_en,unidentified_name,search_en,search_unidentified) VALUES($id,$name,$search,$name,'',$search,'') ON CONFLICT(id) DO UPDATE SET name_en=excluded.name_en,search_en=excluded.search_en";c.Parameters.Add("$id",SqliteType.Integer);c.Parameters.Add("$name",SqliteType.Text);c.Parameters.Add("$search",SqliteType.Text);foreach(var item in items.Values){c.Parameters["$id"].Value=item.Id;c.Parameters["$name"].Value=item.Name;c.Parameters["$search"].Value=MvpSearchCoordinator.Normalize(item.Name);c.ExecuteNonQuery();}tx.Commit();}
   var lua=Path.Combine(source,"bRO.lua");if(File.Exists(lua))FarmlandLuaImporter.Import(lua,temp,serverSource:false);var farmland=Path.Combine(source,"farmland.lua");if(File.Exists(farmland))FarmlandLuaImporter.Import(farmland,temp);
   int metadata=BuildEquipmentCatalog.Import(Path.Combine(source,"item_db_equip.yml"),Path.Combine(source,"item_db_etc.yml"),meta);int total;
   using(var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=temp,Pooling=false}.ToString())){db.Open();using var c=db.CreateCommand();c.CommandText="PRAGMA integrity_check";if((string?)c.ExecuteScalar()!="ok")throw new InvalidDataException("SQLite inválido");c.CommandText="SELECT COUNT(*) FROM items";total=Convert.ToInt32(c.ExecuteScalar());c.CommandText="SELECT COUNT(*) FROM items WHERE id=$id";c.Parameters.Add("$id",SqliteType.Integer);var expected=items.Keys.ToHashSet();foreach(var file in new[]{lua,farmland}.Where(File.Exists))expected.UnionWith(LuaItemNameReader.Read(file).Keys);foreach(int id in expected){c.Parameters["$id"].Value=id;if(Convert.ToInt32(c.ExecuteScalar())!=1)throw new InvalidDataException("Item ausente: "+id);}}
   File.Move(temp,database,true);File.Move(meta,equipment,true);return $"Catálogo completo: {total} itens; {items.Count} IDs YAML; {metadata} metadados de equipamento/carta/ovo. Todos os IDs YAML e Lua conferidos. Drops, mapas e quests preservados.";
  }finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(meta))File.Delete(meta);}
 }
}
