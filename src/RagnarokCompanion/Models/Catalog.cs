using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagnarokCompanion.Models;

public sealed class CatalogEntry
{
    public string Key { get; set; } = "";
    public int MonsterId { get; set; }
    public string Name { get; set; } = "";
    public string Map { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string Origin { get; set; } = "Informação não verificada";
    public double? MinMinutes { get; set; }
    public double? MaxMinutes { get; set; }
    public double? CustomMin { get; set; }
    public double? CustomMax { get; set; }
    public bool FarmlandConfirmed { get; set; }
    public bool Favorite { get; set; }
    public string SourceUrl { get; set; } = "";
    public string Notes { get; set; } = "";
    [JsonIgnore] public bool DisambiguateMap { get; set; }
    [JsonIgnore] public string DisplayName => Name + (DisambiguateMap && !string.IsNullOrWhiteSpace(Map) ? " • " + Map : "");
    public DateTimeOffset? SyncedUtc { get; set; }
    [JsonIgnore] public string DisplayOrigin => CustomMin != null ? FarmlandConfirmed ? "Farmland — confirmado pelo usuário" : "Personalizado pelo usuário" : Origin;
    [JsonIgnore] public string DisplayMap => string.IsNullOrWhiteSpace(Map) ? "Mapa não informado" : Map;
    [JsonIgnore] public string Summary => Name + " • " + DisplayMap;
    [JsonIgnore] public string FavoriteLabel => Favorite ? "★ Favorito" : "☆ Favoritar";
    [JsonIgnore] public string FavoriteMark => Favorite ? "★" : "☆";
    [JsonIgnore] public string RespawnLabel => CustomMin != null ? $"{CustomMin:0.##}–{CustomMax:0.##} min" : MinMinutes != null ? $"{MinMinutes:0.##}–{MaxMinutes:0.##} min (gateway; confirmar no Farmland)" : "Configure apenas o respawn para iniciar";
}
public sealed class IntegrationSettings
{
    public string Gateway { get; set; } = "";
    public DateTimeOffset? LastSyncUtc { get; set; }
}
public sealed class Gateway
{
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("group")] public string[] Groups { get; set; } = Array.Empty<string>();
    [JsonIgnore] public string Label => Slug + " • Renewal";
}
public sealed class GatewayResponse
{
    [JsonPropertyName("gateways")] public List<Gateway>? Gateways { get; set; }
}
