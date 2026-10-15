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
        var choices = store.Load<List<CatalogEntry>>("catalog-choices.json");
        foreach (var choice in choices.Where(ValidChoice))
        {
            choice.Map ??= "";
            var e = Entries.FirstOrDefault(x => x.Key == choice.Key);
            if (e == null) Entries.Add(choice); else CopyChoices(choice, e);
        }
    }
    private static bool ValidChoice(CatalogEntry? c) => c != null && !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Name) && c.MonsterId > 0 && (c.CustomMin == null || MvpRespawnService.Valid(c.CustomMin.Value, c.CustomMax ?? -1));
    private static void CopyChoices(CatalogEntry from, CatalogEntry to)
    { to.Favorite = from.Favorite; to.CustomMin = from.CustomMin; to.CustomMax = from.CustomMax; to.FarmlandConfirmed = from.FarmlandConfirmed; if (!string.IsNullOrWhiteSpace(from.Map)) to.Map = from.Map; }
    public void Merge(IEnumerable<CatalogEntry> entries)
    {
        foreach (var incoming in entries)
        {
            var old = Entries.FirstOrDefault(e => e.Key == incoming.Key);
            var overrides = Entries.Where(e => e.MonsterId == incoming.MonsterId && e.CustomMin != null && (string.IsNullOrWhiteSpace(e.Map) || e.Map == incoming.Map)).OrderByDescending(e => e.FarmlandConfirmed).FirstOrDefault();
            if (old != null) { CopyChoices(old, incoming); Entries.Remove(old); }
            if (overrides != null && (incoming.CustomMin == null || overrides.FarmlandConfirmed))
            { bool favorite = incoming.Favorite || overrides.Favorite; CopyChoices(overrides, incoming); incoming.Favorite = favorite; }
            Entries.Add(incoming);
        }
    }
    public void SaveChoices()
    {
        // Save only explicitly chosen bookmarks/overrides. No API payload, sprite or generic respawn cache on disk.
        var choices = Entries.Where(e => e.Favorite || e.CustomMin != null).Select(e => new CatalogEntry
        { Key = e.Key, MonsterId = e.MonsterId, Name = e.Name, Map = e.Map, Gateway = e.Gateway, Favorite = e.Favorite, CustomMin = e.CustomMin, CustomMax = e.CustomMax, FarmlandConfirmed = e.FarmlandConfirmed, Origin = "Informação não verificada", SourceUrl = e.SourceUrl });
        store.Save("catalog-choices.json", choices.ToList());
    }
}
public interface IMvpRespawnService { (double Min, double Max, string Origin)? Resolve(CatalogEntry entry, bool acceptGateway); }
public sealed class MvpRespawnService : IMvpRespawnService
{
    public static bool Valid(double min, double max) => double.IsFinite(min) && double.IsFinite(max) && min > 0 && min <= max && max <= 10080;
    public (double Min, double Max, string Origin)? Resolve(CatalogEntry e, bool acceptGateway)
    {
        // A Farmland-confirmed user override always wins over generic gateway data.
        if (e.CustomMin is { } min && e.CustomMax is { } max && Valid(min, max)) return (min, max, e.DisplayOrigin);
        if (acceptGateway && e.MinMinutes is { } apiMin && e.MaxMinutes is { } apiMax && Valid(apiMin, apiMax)) return (apiMin, apiMax, e.Origin + " (não confirmado no Farmland)");
        return null;
    }
}
