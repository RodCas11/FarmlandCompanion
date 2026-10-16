using System;
using System.Globalization;
using System.Linq;
using System.Text;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class MvpSearchCoordinator {
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
}
