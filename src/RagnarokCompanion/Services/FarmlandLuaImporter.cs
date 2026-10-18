using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
namespace RagnarokCompanion.Services;
public static class FarmlandLuaImporter {
 public static void Import(string source,string database,IEnumerable<int>? onlyIds=null,bool serverSource=true){
  var entries=LuaItemNameReader.Read(source);
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false}.ToString());db.Open();using var transaction=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=transaction;
  cmd.CommandText="CREATE TABLE IF NOT EXISTS item_descriptions(item_id INTEGER PRIMARY KEY REFERENCES items(id),raw_text TEXT NOT NULL,source TEXT NOT NULL); CREATE TABLE IF NOT EXISTS item_description_overrides(item_id INTEGER PRIMARY KEY REFERENCES items(id),raw_text TEXT NOT NULL)";cmd.ExecuteNonQuery();
  foreach(var entry in entries.Where(e=>onlyIds==null||onlyIds.Contains(e.Key))){
   cmd.Parameters.Clear();cmd.CommandText="INSERT INTO items(id,name,search_name,name_en,unidentified_name,search_en,search_unidentified) VALUES($id,$name,$search,'',$unknown,'',$searchUnknown) ON CONFLICT(id) DO UPDATE SET name=excluded.name,search_name=excluded.search_name,unidentified_name=excluded.unidentified_name,search_unidentified=excluded.search_unidentified";
   cmd.Parameters.AddWithValue("$id",entry.Key);cmd.Parameters.AddWithValue("$name",entry.Value.Identified);cmd.Parameters.AddWithValue("$search",MvpSearchCoordinator.Normalize(entry.Value.Identified));cmd.Parameters.AddWithValue("$unknown",entry.Value.Unidentified);cmd.Parameters.AddWithValue("$searchUnknown",MvpSearchCoordinator.Normalize(entry.Value.Unidentified));cmd.ExecuteNonQuery();
   if(!string.IsNullOrWhiteSpace(entry.Value.Description)){cmd.Parameters.Clear();cmd.CommandText=serverSource?"INSERT INTO item_description_overrides(item_id,raw_text) VALUES($id,$raw) ON CONFLICT(item_id) DO UPDATE SET raw_text=excluded.raw_text":"INSERT INTO item_descriptions(item_id,raw_text,source) VALUES($id,$raw,'bRO.lua') ON CONFLICT(item_id) DO UPDATE SET raw_text=excluded.raw_text,source=excluded.source";cmd.Parameters.AddWithValue("$id",entry.Key);cmd.Parameters.AddWithValue("$raw",entry.Value.Description);cmd.ExecuteNonQuery();}
  }
  transaction.Commit();
 }
}
