using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RagnarokCompanion.Services;
public sealed class ItemMonsterRules {
 public List<int> ExcludeIds {get;set;}=new(); public List<int> IncludeIds {get;set;}=new();
 public List<string> ExcludedNamePatterns {get;set;}=new(){"(^|[ _-])(Furious|Swift|Solid|Ringleader|Champion|Event|Evento|Summoned|Invocado|Quest)([ _-]|$)","^(Furios[oa]|Veloz|S[oó]lid[oa]|L[ií]der) "};
 public List<string> ExcludedAegisPatterns {get;set;}=new(){"^(C[1-5]_|G_|I_|EVENT_|QUEST_|SUMMON_)"};
 public static ItemMonsterRules Load(string? path=null){path??=Path.Combine(AppContext.BaseDirectory,"Data","item-monster-rules.json");return File.Exists(path)?JsonSerializer.Deserialize<ItemMonsterRules>(File.ReadAllText(path))??throw new InvalidDataException("Regras de monstros inválidas."):new();}
 public bool Excluded(int id,string name,string aegis="",bool classified=false){if(ExcludeIds.Contains(id))return true;if(IncludeIds.Contains(id))return false;return classified||Matches(ExcludedNamePatterns,name)||Matches(ExcludedAegisPatterns,aegis);}
 static bool Matches(List<string> patterns,string value){foreach(var pattern in patterns)if(Regex.IsMatch(value,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))return true;return false;}
}
