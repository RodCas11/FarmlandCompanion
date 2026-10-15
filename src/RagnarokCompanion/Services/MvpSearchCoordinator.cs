using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;

public sealed class RagnaPlaceMonsterSearchService
{
    readonly IRagnaPlaceApiClient api;
    public RagnaPlaceMonsterSearchService(IRagnaPlaceApiClient api) => this.api = api;
    public async Task<List<CatalogEntry>> SearchAsync(string gateway, string query, int page, bool refresh, CancellationToken ct)
    {
        if (int.TryParse(query.Trim(), out int id))
            return await new RagnaPlaceCatalogProvider(api).DetailsAsync(gateway, id, refresh, ct);
        string term=MvpSearchCoordinator.Normalize(query.Trim());
        var result = await api.SearchAsync(gateway, term, page, refresh, ct);
        string alias=MvpSearchCoordinator.Alternate(query);
        if(result.Items!.Count==0&&!string.Equals(term,alias,StringComparison.OrdinalIgnoreCase))
            result=await api.SearchAsync(gateway,alias,page,refresh,ct);
        return result.Items!.GroupBy(m => m.Id).Select(g => new CatalogEntry {
            Key=$"search:{gateway}:{g.Key}", MonsterId=g.Key!.Value, Name=g.First().Name!, Gateway=gateway,
            Origin="RagnaPlace", SourceUrl="https://ragnaplace.com", IsSearchCandidate=true
        }).ToList();
    }
}

public sealed class MvpSearchCoordinator : IDisposable
{
    readonly RagnaPlaceMonsterSearchService search;
    CancellationTokenSource? current;
    public MvpSearchCoordinator(IRagnaPlaceApiClient api) => search=new(api);
    public void Cancel() { current?.Cancel(); }
    public async Task<List<CatalogEntry>?> SearchAsync(string gateway,string query,int page=1,bool refresh=false,bool debounce=true)
    {
        Cancel(); var source=new CancellationTokenSource();current=source;
        try {
            if(debounce)await Task.Delay(400,source.Token);
            var rows=await search.SearchAsync(gateway,query,page,refresh,source.Token);
            source.Token.ThrowIfCancellationRequested();return rows;
        }
        catch(Exception) when(source.IsCancellationRequested){return null;}
        finally { if(ReferenceEquals(current,source))current=null;source.Dispose(); }
    }
    public static string Normalize(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    static readonly (string Alias,string Name)[] aliases = { ("freeoni","Phreeoni"),("senhor das trevas","Dark Lord"),("heroi orc","Orc Hero"),("senhor dos orcs","Orc Lord"),("abelha rainha","Mistress") };
    public static string Canonical(string text) { var normalized=Normalize(text.Trim());return aliases.FirstOrDefault(a=>a.Alias==normalized).Name??Normalize(text.Trim()); }
    public static string Alternate(string text) {
        string q=Normalize(text.Trim());
        var matches=aliases.Where(a=>a.Alias==q||Normalize(a.Name)==q||(q.Length>=3&&(a.Alias.StartsWith(q)||Normalize(a.Name).StartsWith(q)))).ToList();
        if(matches.Count!=1)return q;
        var match=matches[0];return Normalize(match.Name).StartsWith(q)?match.Alias:Normalize(match.Name);
    }
    public static bool Matches(CatalogEntry entry,string query) {
        string q=Normalize(query.Trim());
        return q.Length==0||entry.MonsterId.ToString(CultureInfo.InvariantCulture)==q||Normalize(entry.Name).Contains(q)||Normalize(entry.Name).Contains(Canonical(query))||aliases.Any(a=>Normalize(entry.Name)==Normalize(a.Name)&&a.Alias.Contains(q));
    }
    public void Dispose(){Cancel();}
}
