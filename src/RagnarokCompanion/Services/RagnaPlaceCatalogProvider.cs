using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
using RagnarokCompanion.Models.Api;

namespace RagnarokCompanion.Services;
public interface IRagnaPlaceCatalogProvider
{
    Task<SearchResultDto> SearchAsync(string gateway, string query, int page, bool refresh = false, CancellationToken ct = default);
    Task<List<CatalogEntry>> DetailsAsync(string gateway, int id, bool refresh = false, CancellationToken ct = default);
}
public sealed class RagnaPlaceCatalogProvider : IRagnaPlaceCatalogProvider
{
    private readonly IRagnaPlaceApiClient api;
    public RagnaPlaceCatalogProvider(IRagnaPlaceApiClient api) => this.api = api;
    public Task<SearchResultDto> SearchAsync(string gateway, string query, int page, bool refresh = false, CancellationToken ct = default) => api.SearchAsync(gateway, query, page, refresh, ct);
    public async Task<List<CatalogEntry>> DetailsAsync(string gateway, int id, bool refresh = false, CancellationToken ct = default)
    {
        var mob = await api.MonsterAsync(gateway, id, refresh, ct);
        // Boss class alone is NOT evidence of MVP status.
        if (mob.Exp?.Mvp is not > 0) throw new RagnaPlaceException("A resposta não confirma este monstro como MVP (EXP MVP ausente/zero). Use o catálogo local verificado ou cadastro personalizado.");
        var maps = mob.Spawns is { Count: > 0 } ? mob.Spawns : new List<MapSpawnDto> { new() };
        return maps.Select(s => new CatalogEntry
        {
            Key = $"api:{gateway}:{id}:{s.Map?.Id ?? "unknown"}", MonsterId = id, Name = mob.Name!, Map = s.Map?.Id ?? "", Gateway = gateway,
            Origin = "RagnaPlace — " + (mob.GatewayId ?? gateway),
            IsSpecialEncounter=(s.Map?.Id?.Contains('@')??false)||(mob.Categories??new()).Concat(mob.Subcategories??new()).Any(tag=>tag!=null&&(tag.Contains("instance",StringComparison.OrdinalIgnoreCase)||tag.Contains("event",StringComparison.OrdinalIgnoreCase))),
            AmbiguousSpawn=maps.Where(other=>other.Map?.Id==s.Map?.Id).Select(other=>other.RespawnSeconds).Distinct().Count()>1,
            MinMinutes = s.RespawnSeconds is > 0 and <= 604800 ? s.RespawnSeconds.Value / 60d : null,
            MaxMinutes = s.RespawnSeconds is > 0 and <= 604800 ? s.RespawnSeconds.Value / 60d : null,
            SourceUrl = mob.Url ?? "https://ragnaplace.com", SyncedUtc = DateTimeOffset.UtcNow,
            Notes = "MVP identificado por exp.mvp > 0. Respawn do gateway em segundos, convertido para minutos. A API não informa limite máximo variável; confirme a janela do Farmland."
        }).GroupBy(x => x.Key).Select(g => g.First()).ToList();
    }
}
public interface IRagnaPlaceAuthService { Task TestAsync(string gateway, CancellationToken ct = default); }
public sealed class RagnaPlaceAuthService : IRagnaPlaceAuthService
{
    private readonly IRagnaPlaceApiClient api;
    public RagnaPlaceAuthService(IRagnaPlaceApiClient api) => this.api = api;
    // Gateways is unauthenticated; use a small authenticated search to verify the key.
    public async Task TestAsync(string gateway, CancellationToken ct = default) => await api.SearchAsync(gateway, "Poring", 1, true, ct);
}
