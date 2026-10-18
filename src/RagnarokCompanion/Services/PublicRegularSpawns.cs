using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed class PublicRegularSpawns {
 public sealed class Record {public int MonsterId {get;set;}public string MapCode {get;set;}="";public string MapName {get;set;}="";public int Count {get;set;}public string SourceUrl {get;set;}="";}
 public string SourceVersion {get;set;}="";public List<Record> Records {get;set;}=new();
 static readonly Lazy<PublicRegularSpawns> shared=new(()=>{using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RagnarokCompanion.Data.public-regular-spawns.json")??throw new InvalidDataException("Referência pública de mapas ausente.");return JsonSerializer.Deserialize<PublicRegularSpawns>(stream)??throw new InvalidDataException("Referência de mapas inválida.");});
 public static PublicRegularSpawns Shared=>shared.Value;
 public bool Regular(int monster,string map)=>Records.Any(r=>r.MonsterId==monster&&r.MapCode==map);
 public List<FarmSpawn> ForMonster(int monster)=>Records.Where(r=>r.MonsterId==monster).Select(r=>new FarmSpawn{Map=r.MapCode,Name=r.MapName,Count=r.Count,Evidence="rAthena Renewal — referência pública; não confirmado no Farmland. "+r.SourceUrl}).ToList();
}
