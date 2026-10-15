using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace RagnarokCompanion.Models;
public sealed class SpotlightCatalog
{
    public int Version { get; set; } = 1;
    public string SourceUrl { get; set; } = "https://farmland.net.br/wiki/index.php/SpotLight";
    public long Revision { get; set; }
    public DateTimeOffset RetrievedUtc { get; set; }
    public List<SpotlightGroup> Groups { get; set; } = new();
}
public sealed class SpotlightGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<SpotlightMap> Maps { get; set; } = new();
    [JsonIgnore] public string Summary => Name + " — " + Maps.Count + " mapas";
}
public sealed class SpotlightMap
{
    public string GroupId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int? MinLevel { get; set; }
    public int? MaxLevel { get; set; }
    public int? Recommended { get; set; }
    public List<SpotlightMonster> Monsters { get; set; } = new();
    [JsonIgnore] public string DivinePrideUrl => "https://www.divine-pride.net/database/map/" + Uri.EscapeDataString(Code);
    [JsonIgnore] public string MonsterInfo => Monsters.Count == 0 ? "Monstros não informados na fonte." : string.Join(Environment.NewLine, Monsters.Select(m => $"{m.Name} — Nv. {m.Level}{(string.IsNullOrEmpty(m.Tag) ? "" : " (" + m.Tag + ")")}"));
}
public sealed class SpotlightMonster
{
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string Tag { get; set; } = "";
}
public sealed class SpotlightSelection
{
    public string GroupId { get; set; } = "";
    public DateTimeOffset? SelectedUtc { get; set; }
    public int? CharacterLevel { get; set; }
    public bool ShowCompact { get; set; } = true;
}
public sealed record SpotlightLevelFilter(string Label, int? Min, int? Max);
