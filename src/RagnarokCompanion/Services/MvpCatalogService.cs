using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;
public interface IMvpCatalogService
{
    List<CatalogEntry> Entries { get; }
    void Merge(IEnumerable<CatalogEntry> entries);
    void SaveChoices();
}
public sealed class MvpCatalogService : IMvpCatalogService
{
    public List<CatalogEntry> Entries { get; }
    private readonly PersistenceService store;
    public MvpCatalogService(PersistenceService store)
    {
        this.store = store;
        using var stream = typeof(MvpCatalogService).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.mvp-seed.json")!;
        Entries = JsonSerializer.Deserialize<List<CatalogEntry>>(stream) ?? new();
        foreach(var group in new MvpRespawnCatalog(store).Records.GroupBy(r=>r.MonsterId)){
            var identity=Entries.FirstOrDefault(e=>e.MonsterId==group.Key);
            var points=group.GroupBy(r=>(r.MapCode,r.SpawnIdentifier)).Select(g=>g.OrderByDescending(r=>r.IsFarmlandConfirmed).First()).ToList();
            if(identity!=null&&points.Count==1){identity.Map=points[0].MapCode;identity.SpawnIdentifier=points[0].SpawnIdentifier;if(points[0].IsFarmlandConfirmed){identity.Name=points[0].MonsterName;identity.Origin="Farmland confirmado";}continue;}
            if(identity!=null)Entries.Remove(identity);
            foreach(var point in points)Entries.Add(new CatalogEntry{Key=$"spawn:{point.MonsterId}:{point.MapCode}:{point.SpawnIdentifier}",MonsterId=point.MonsterId,Name=identity?.Name??point.MonsterName,Map=point.MapCode,SpawnIdentifier=point.SpawnIdentifier,Origin=point.IsFarmlandConfirmed?"Farmland confirmado":"Referência Renewal — rAthena",SourceUrl=point.SourceUrl});
        }
        var choices = store.Load<List<CatalogEntry>>("catalog-choices.json");
        foreach (var choice in choices.Where(ValidChoice))
        {
            choice.Map ??= "";
            var e = Entries.FirstOrDefault(x => x.Key == choice.Key);
            if(e!=null&&e.Origin.StartsWith("Farmland",StringComparison.Ordinal)&&!string.IsNullOrEmpty(choice.Map)&&choice.Map!=e.Map){e.Key=$"farmland:{e.MonsterId}:{e.Map}";e=null;}
            e??=Entries.FirstOrDefault(x=>x.MonsterId==choice.MonsterId&&x.Map==choice.Map&&!x.Retained&&!x.Favorite);
            if(e!=null&&e.Key!=choice.Key)e.Key=choice.Key;
            if (e == null) Entries.Add(choice); else CopyChoices(choice, e);
        }
    }
    private static bool ValidChoice(CatalogEntry? c) => c != null && !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Name) && (c.MonsterId > 0 || c.Key.StartsWith("manual:",StringComparison.Ordinal)) && (c.CustomMin == null || MvpRespawnService.Valid(c.CustomMin.Value, c.CustomMax ?? -1));
    private static void CopyChoices(CatalogEntry from, CatalogEntry to,bool includeReference=true)
    { if(includeReference){to.SavedReferenceMin=from.SavedReferenceMin;to.SavedReferenceMax=from.SavedReferenceMax;to.SavedReferenceOrigin=from.SavedReferenceOrigin;}to.Retained = from.Retained; to.Favorite = from.Favorite; to.CustomMin = from.CustomMin; to.CustomMax = from.CustomMax; to.FarmlandConfirmed = from.FarmlandConfirmed; if (!string.IsNullOrWhiteSpace(from.Map)) to.Map = from.Map; }
    public void Merge(IEnumerable<CatalogEntry> entries)
    {
        foreach (var incoming in entries)
        {
            var old = Entries.FirstOrDefault(e => e.Key == incoming.Key);
            var overrides = Entries.Where(e => e.MonsterId == incoming.MonsterId && e.CustomMin != null && (string.IsNullOrWhiteSpace(e.Map) || e.Map == incoming.Map)).OrderByDescending(e => e.FarmlandConfirmed).FirstOrDefault();
            if (old != null) { CopyChoices(old, incoming); Entries.Remove(old); }
            if (overrides != null && (incoming.CustomMin == null || overrides.FarmlandConfirmed))
            { bool favorite = incoming.Favorite || overrides.Favorite; CopyChoices(overrides, incoming,false); incoming.Favorite = favorite; }
            Entries.Add(incoming);
        }
    }
    public void SaveChoices()
    {
        // Save only explicitly chosen bookmarks/overrides. No API payload, sprite or generic respawn cache on disk.
        var choices = Entries.Where(e => !e.IsSearchCandidate && (e.Retained || e.Favorite || e.CustomMin != null)).Select(e => new CatalogEntry
        { Key = e.Key, MonsterId = e.MonsterId, Name = e.Name, Map = e.Map, SpawnIdentifier=e.SpawnIdentifier, IsSpecialEncounter=e.IsSpecialEncounter, AmbiguousSpawn=e.AmbiguousSpawn, Gateway = e.Gateway, Retained=e.Retained, Favorite = e.Favorite, CustomMin = e.CustomMin, CustomMax = e.CustomMax, FarmlandConfirmed = e.FarmlandConfirmed, Origin = e.Origin, SourceUrl = e.SourceUrl, SavedReferenceMin=e.MinMinutes??e.SavedReferenceMin,SavedReferenceMax=e.MaxMinutes??e.SavedReferenceMax,SavedReferenceOrigin=e.MinMinutes!=null?e.Origin:e.SavedReferenceOrigin });
        store.Save("catalog-choices.json", choices.ToList());
    }
}
public interface IMvpRespawnService { (double Min, double Max, string Origin)? Resolve(CatalogEntry entry, bool acceptGateway); }
public sealed class MvpRespawnService : IMvpRespawnService
{
    public MvpRespawnCatalog Catalog {get;}
    public MvpRespawnService(MvpRespawnCatalog? catalog=null)=>Catalog=catalog??new();
    public static bool Valid(double min, double max) => double.IsFinite(min) && double.IsFinite(max) && min > 0 && min <= max && max <= 10080;
    public (double Min, double Max, string Origin)? Resolve(CatalogEntry e, bool acceptGateway)
    {
        // A Farmland-confirmed user override always wins over generic gateway data.
        if (e.CustomMin is { } min && e.CustomMax is { } max && Valid(min, max)) return (min, max, e.DisplayOrigin);
        var specific=Catalog.Find(e,true);
        if(specific!=null)return Use(specific,e);
        if(e.IsSpecialEncounter||e.AmbiguousSpawn||e.Map.Contains('@'))return null;
        if ((acceptGateway||!string.IsNullOrWhiteSpace(e.Gateway)) && e.MinMinutes is { } apiMin && e.MaxMinutes is { } apiMax && Valid(apiMin, apiMax)) return (apiMin, apiMax, e.Origin + " • referência do gateway, não confirmada no Farmland");
        if(!string.IsNullOrWhiteSpace(e.Gateway)&&e.SavedReferenceMin is {} savedMin&&e.SavedReferenceMax is {} savedMax&&Valid(savedMin,savedMax))return(savedMin,savedMax,e.SavedReferenceOrigin+" • referência do gateway salva, não confirmada no Farmland");
        var reference=Catalog.Find(e);return reference==null?null:Use(reference,e);
    }
    static (double Min,double Max,string Origin) Use(MvpRespawnRecord r,CatalogEntry entry){if(string.IsNullOrEmpty(entry.Map))entry.Map=r.MapCode;return(r.RespawnMinSeconds/60,r.RespawnMaxSeconds/60,(r.IsFarmlandConfirmed?"Farmland confirmado • ":"Referência Renewal • ")+r.Source+" @ "+r.SourceVersion[..Math.Min(8,r.SourceVersion.Length)]);}
}
