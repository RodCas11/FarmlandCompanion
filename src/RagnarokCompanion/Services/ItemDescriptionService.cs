using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
namespace RagnarokCompanion.Services;
public sealed record ItemDescription(string RawText,string Source);
public sealed class ItemDescriptionService {
 readonly string database;readonly ConcurrentDictionary<int,Lazy<Task<ItemDescription>>> cache=new();
 public ItemDescriptionService(string? database=null)=>this.database=database??ItemFarmIndex.DatabasePath;
 public Task<ItemDescription> GetAsync(int id){if(cache.Count>=64)foreach(var key in cache.Keys.Take(16))cache.TryRemove(key,out _);return cache.GetOrAdd(id,key=>new(()=>Task.Run(()=>Read(key)))).Value;}
 ItemDescription Read(int id){using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());db.Open();using var command=db.CreateCommand();command.Parameters.AddWithValue("$id",id);
  foreach(var table in new[]{"item_description_overrides","item_descriptions"}){command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$table";command.Parameters.AddWithValue("$table",table);bool exists=Convert.ToInt32(command.ExecuteScalar())>0;command.Parameters.RemoveAt("$table");if(!exists)continue;command.CommandText="SELECT raw_text FROM "+table+" WHERE item_id=$id";if(command.ExecuteScalar() is string raw&&!string.IsNullOrWhiteSpace(raw))return new(raw,table=="item_description_overrides"?"Farmland · fonte atualizada autorizada pelo ADM":"bRO fornecido · modificações Farmland não confirmadas");}
  return new("Descrição local não disponível para este item.","Sem descrição confirmada do Farmland");
 }
 public sealed class AuthorizedDescription {public int Id {get;set;}public string Description {get;set;}="";}
 public static void ImportAuthorized(string database,string json){
  var records=JsonSerializer.Deserialize<List<AuthorizedDescription>>(File.ReadAllText(json))??throw new InvalidDataException("Descrições inválidas.");
  if(records.Any(r=>r.Id<=0||string.IsNullOrWhiteSpace(r.Description)||r.Description.Length>100000)||records.Select(r=>r.Id).Distinct().Count()!=records.Count)throw new InvalidDataException("IDs/descrições vazios, duplicados ou inválidos.");
  using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Mode=SqliteOpenMode.ReadWrite,Pooling=false,ForeignKeys=true}.ToString());db.Open();using var transaction=db.BeginTransaction();using var command=db.CreateCommand();command.Transaction=transaction;command.CommandText="CREATE TABLE IF NOT EXISTS item_description_overrides(item_id INTEGER PRIMARY KEY REFERENCES items(id),raw_text TEXT NOT NULL)";command.ExecuteNonQuery();command.CommandText="INSERT INTO item_description_overrides VALUES($id,$raw) ON CONFLICT(item_id) DO UPDATE SET raw_text=excluded.raw_text";command.Parameters.Add("$id",SqliteType.Integer);command.Parameters.Add("$raw",SqliteType.Text);foreach(var record in records){command.Parameters["$id"].Value=record.Id;command.Parameters["$raw"].Value=record.Description;command.ExecuteNonQuery();}transaction.Commit();
 }
}
