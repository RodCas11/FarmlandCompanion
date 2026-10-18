using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagnarokCompanion.Models.Api;

// Fields and units from the official /v1/openapi.json (15 October 2026).
public sealed class MobRefDto
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("image")] public MobImageDto? Image { get; set; }
}
public sealed class MobImageDto
{
    [JsonPropertyName("static")] public string? Static { get; set; }
}
public sealed class SearchResultDto
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("gateway")] public string? Gateway { get; set; }
    [JsonPropertyName("page")] public int? Page { get; set; }
    [JsonPropertyName("pages")] public int? Pages { get; set; }
    [JsonPropertyName("items")] public List<MobRefDto>? Items { get; set; }
}
public sealed class MobDto
{
    [JsonPropertyName("level")] public int? Level {get;set;}
    [JsonPropertyName("categories")] public List<string>? Categories {get;set;}
    [JsonPropertyName("subcategories")] public List<string>? Subcategories {get;set;}
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("gatewayId")] public string? GatewayId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("class")] public string? Class { get; set; }
    public int SpawnSchemaVersion {get;set;} public List<RagnarokCompanion.Models.FarmSpawn>? MapCandidates {get;set;}
    public string? Description {get;set;} public string? IconUrl {get;set;}
    [JsonPropertyName("exp")] public ExperienceDto? Exp { get; set; }
    [JsonPropertyName("image")] public MobImageDto? Image { get; set; }
    [JsonPropertyName("spawns")] public List<MapSpawnDto>? Spawns { get; set; }
}
public sealed class ExperienceDto { [JsonPropertyName("mvp")] public long? Mvp { get; set; } }
public sealed class MapSpawnDto
{
    [JsonPropertyName("map")] public MapRefDto? Map { get; set; }
    [JsonPropertyName("respawn")] public int? RespawnSeconds { get; set; }
}
public sealed class MapRefDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}
