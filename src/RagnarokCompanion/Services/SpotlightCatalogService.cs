using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;
public interface ISpotlightCatalogService
{
    SpotlightCatalog? LoadLocal();
    Task<SpotlightCatalog> RefreshAsync(CancellationToken cancellationToken = default);
}
public sealed class SpotlightCatalogService : ISpotlightCatalogService, IDisposable
{
    public const string SourceUrl = "https://farmland.net.br/wiki/index.php/SpotLight";
    private readonly PersistenceService store;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRefresh;
    private readonly bool preferBundled;
    public SpotlightCatalogService(PersistenceService store, HttpClient? http = null, bool preferBundled = true)
    { this.store = store; this.preferBundled=preferBundled;this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }); }
    public SpotlightCatalog? LoadLocal()
    {
        var data = File.Exists(Path.Combine(store.Root, "spotlight-catalog.json"))?store.Load<SpotlightCatalog>("spotlight-catalog.json"):null;
        if(!Valid(data))data=null;
        // Existing verified catalogs used this exact Divine Pride route. Preserve those links during migration.
        if(data!=null&&!data.ExplicitMapLinks&&data.Revision>0)foreach(var map in data.Groups.SelectMany(g=>g.Maps))map.MapUrl??="https://www.divine-pride.net/database/map/"+Uri.EscapeDataString(map.Code);
        return preferBundled?SpotlightExtractedCatalog.Supplement(SpotlightExtractedCatalog.LoadBundled(),data):data;
    }
    public async Task<SpotlightCatalog> RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (nextRefresh > DateTimeOffset.UtcNow) throw new InvalidOperationException("Aguarde um minuto antes de atualizar a wiki novamente. O catálogo local continua disponível.");
            nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
            using var response = await http.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Wiki indisponível (HTTP {(int)response.StatusCode}). O catálogo local foi preservado.");
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream(); byte[] block = new byte[8192]; int read;
            while ((read = await source.ReadAsync(block, timeout.Token)) > 0)
            { if (buffer.Length + read > 2_000_000) throw new InvalidDataException("Página maior que o limite de segurança."); buffer.Write(block, 0, read); }
            var catalog = Parse(Encoding.UTF8.GetString(buffer.ToArray()), DateTimeOffset.UtcNow);
            if(preferBundled)catalog=SpotlightExtractedCatalog.Supplement(SpotlightExtractedCatalog.LoadBundled(),catalog);
            store.Save("spotlight-catalog.json", catalog);
            return catalog;
        }
        finally { gate.Release(); }
    }
    // Read JSON literals only. No JavaScript from the website is ever executed by the app.
    public static SpotlightCatalog Parse(string html, DateTimeOffset retrievedUtc)
    {
        if (html.Length > 2_000_000) throw new InvalidDataException("Página muito grande.");
        var options = Regex.Matches(html, @"<option\s+value=""(Spotlight [^""]+)""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).Distinct().ToList();
        var listsMatch = Regex.Match(html, @"const\s+lists\s*=\s*\{(?<body>[\s\S]*?)\};", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var bindings = Regex.Matches(listsMatch.Groups["body"].Value, @"""(?<name>Spotlight [^""]+)""\s*:\s*(?<variable>[a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!html.Contains("Math.max(1,max-15)", StringComparison.Ordinal) || bindings.Count == 0 || options.Count != bindings.Count) throw new InvalidDataException("A estrutura ou a regra de níveis da wiki mudou. O catálogo anterior foi preservado; é preciso revisar a fonte.");
        using var monsters = JsonDocument.Parse(Literal(html, "monsters", '{', '}'));
        if (monsters.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Monstros em formato inesperado.");
        var result = new SpotlightCatalog { RetrievedUtc = retrievedUtc, ExplicitMapLinks=true };
        var revision = Regex.Match(html, @"""wgRevisionId""\s*:\s*(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (long.TryParse(revision.Groups[1].Value, out long rev)) result.Revision = rev;
        foreach (Match binding in bindings)
        {
            string name = binding.Groups["name"].Value, variable = binding.Groups["variable"].Value;
            if (!options.Contains(name)) throw new InvalidDataException("Grupo divergente do seletor oficial.");
            using var maps = JsonDocument.Parse(Literal(html, variable, '[', ']'));
            var group = new SpotlightGroup { Id = variable, Name = name };
            foreach (var row in maps.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 4) throw new InvalidDataException("Linha de mapa em formato inesperado.");
                var map = new SpotlightMap { GroupId = variable, Code = row[0].GetString() ?? "", Name = row[1].GetString() ?? "", MinLevel = row[2].ValueKind == JsonValueKind.Null ? null : row[2].GetInt32(), MaxLevel = row[3].ValueKind == JsonValueKind.Null ? null : row[3].GetInt32() };
                map.Recommended = map.MaxLevel is { } max ? Math.Max(1, max - 15) : null;
                if(html.Contains("https://www.divine-pride.net/database/map/",StringComparison.Ordinal))map.MapUrl="https://www.divine-pride.net/database/map/"+Uri.EscapeDataString(map.Code);
                if (monsters.RootElement.TryGetProperty(map.Code, out var mobList))
                    foreach (var mob in mobList.EnumerateArray())
                    {
                        if (mob.GetArrayLength() is < 2 or > 3) throw new InvalidDataException("Monstro em formato inesperado.");
                        map.Monsters.Add(new SpotlightMonster { Name = mob[0].GetString() ?? "", Level = mob[1].GetInt32(), Tag = mob.GetArrayLength() == 3 ? mob[2].GetString() ?? "" : "" });
                    }
                group.Maps.Add(map);
            }
            result.Groups.Add(group);
        }
        if (!Valid(result)) throw new InvalidDataException("Catálogo incompleto ou inválido. Nenhum dado anterior foi apagado.");
        return result;
    }
    private static string Literal(string html, string variable, char open, char close)
    {
        var match = Regex.Match(html, @"\bconst\s+" + Regex.Escape(variable) + @"\s*=\s*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        int start = match.Index + match.Length;
        if (!match.Success || start >= html.Length || html[start] != open) throw new InvalidDataException("Lista oficial não encontrada: " + variable);
        // Balance delimiters while respecting quoted strings; never eval script expressions.
        int depth = 0; bool quoted = false, escaped = false;
        for (int i = start; i < html.Length; i++)
        {
            char c = html[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"') { quoted = true; continue; }
            if (c == open) depth++;
            else if (c == close && --depth == 0) return html.Substring(start, i - start + 1);
        }
        throw new InvalidDataException("Lista oficial incompleta: " + variable);
    }
    public static bool Valid(SpotlightCatalog? data) => data != null && data.Version == 1 && data.SourceUrl == SourceUrl && data.Groups != null && data.Groups.Count is > 0 and <= 30 && data.Groups.Select(g => g?.Id).Distinct().Count() == data.Groups.Count && data.Groups.All(g => g != null && !string.IsNullOrWhiteSpace(g.Id) && Regex.IsMatch(g.Id, "^[a-zA-Z][a-zA-Z0-9_]{0,39}$") && !string.IsNullOrWhiteSpace(g.Name) && g.Maps != null && g.Maps.Count is > 0 and <= 500 && g.Maps.Select(m => m?.Code).Distinct().Count() == g.Maps.Count && g.Maps.All(m => m != null && m.GroupId == g.Id && !string.IsNullOrWhiteSpace(m.Code) && Regex.IsMatch(m.Code, "^[a-zA-Z0-9_]{1,40}$") && !string.IsNullOrWhiteSpace(m.Name) && (m.MinLevel == null || m.MinLevel is >= 1 and <= 999) && (m.MaxLevel == null || m.MaxLevel is >= 1 and <= 999) && (m.MinLevel == null || m.MaxLevel == null || m.MinLevel <= m.MaxLevel) && (m.Recommended == null || m.Recommended is >= 1 and <= 999) && m.Monsters != null && m.Monsters.All(n => n != null && !string.IsNullOrWhiteSpace(n.Name) && n.Level is >= 1 and <= 999)));
    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
