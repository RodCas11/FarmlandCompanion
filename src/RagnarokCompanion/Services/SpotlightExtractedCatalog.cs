using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class SpotlightExtractedCatalog
{
    public static SpotlightCatalog LoadBundled()
    {
        using var stream=typeof(SpotlightExtractedCatalog).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.spotlights_farmland_completo.json")!;
        using var reader=new StreamReader(stream);return Parse(reader.ReadToEnd());
    }
    public static SpotlightCatalog Parse(string json)
    {
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        var catalog=new SpotlightCatalog{ExplicitMapLinks=true,DataOrigin="JSON extraído da wiki Farmland",RetrievedUtc=root.GetProperty("extraido_em").GetDateTimeOffset()};
        if(root.GetProperty("fonte").GetString()!=SpotlightCatalogService.SourceUrl)throw new InvalidDataException("Fonte do JSON Spotlight não reconhecida.");
        foreach(var data in root.GetProperty("grupos").EnumerateArray()){
            string name=data.GetProperty("nome").GetString()??"";
            string id=Regex.Replace(name,"^Spotlight\\s+","",RegexOptions.IgnoreCase).ToLowerInvariant();
            id=Regex.Replace(id,"[^a-z0-9_]","_");
            var group=new SpotlightGroup{Id=id,Name=name};
            foreach(var row in data.GetProperty("mapas").EnumerateArray()){
                var map=new SpotlightMap{GroupId=id,Code=Text(row,"codigo")??"",Name=Text(row,"nome")??Text(row,"codigo")??"Mapa sem nome",MinLevel=Number(row,"nivel_minimo"),MaxLevel=Number(row,"nivel_maximo"),Recommended=Number(row,"nivel_recomendado"),MapUrl=Text(row,"link_divine_pride")};
                if(row.TryGetProperty("monstros_tooltip",out var monsters)&&monsters.ValueKind==JsonValueKind.Array)
                    foreach(var monster in monsters.EnumerateArray())if(Text(monster,"nome") is {} mobName&&Number(monster,"nivel") is {} level)map.Monsters.Add(new(){Name=mobName,Level=level,Tag=Text(monster,"informacoes")??""});
                group.Maps.Add(map);
            }
            catalog.Groups.Add(group);
        }
        if(!SpotlightCatalogService.Valid(catalog))throw new InvalidDataException("JSON Spotlight incompleto ou inválido.");
        return catalog;
    }
    static string? Text(JsonElement row,string field)=>row.TryGetProperty(field,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    static int? Number(JsonElement row,string field)=>row.TryGetProperty(field,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out int n)?n:null;
    public static SpotlightCatalog Supplement(SpotlightCatalog primary,SpotlightCatalog? saved)
    {
        if(saved==null)return primary;
        foreach(var map in primary.Groups.SelectMany(g=>g.Maps)){
            var previous=saved.Groups.SelectMany(g=>g.Maps).FirstOrDefault(m=>m.Code==map.Code);
            if(previous==null)continue;
            if(map.Monsters.Count==0)map.Monsters=previous.Monsters.ToList();
            if(map.MapUrl==null)map.MapUrl=previous.MapUrl;
        }
        return primary;
    }
}
