using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
// Reads data through the Lua lexer; never executes Lua. Missing equipment facts retain the base catalog.
public static class BuildFarmlandImporter
{
 public static string Import(string source,string database,string output)
 {
  var entries=LuaItemNameReader.Read(source);var catalog=new BuildEquipmentCatalog();var result=new List<BuildItemMetadata>();
  foreach(var (id,data) in entries){
   var old=catalog.Find(id);var item=old==null?new BuildItemMetadata{Id=id}:JsonSerializer.Deserialize<BuildItemMetadata>(JsonSerializer.Serialize(old))!;
   string text=Regex.Replace(data.Description,@"\^[0-9a-fA-F]{6}","");string Field(string name)=>MvpSearchCoordinator.Normalize(Regex.Match(text,@"(?im)^\s*"+name+@"\s*:\s*(.+)$").Groups[1].Value);
   string type=Field("Tipo"),place=Field("Equipa em"),classes=Field("Classes");bool visual=data.Costume||type.Contains("visual");bool shadow=type.Contains("sombr")||item.Type=="ShadowGear";
   var locations=new Dictionary<string,bool>();void At(string location)=>locations[location]=true;
   if(type=="mascote"||Regex.IsMatch(MvpSearchCoordinator.Normalize(data.Identified),@"^ovo (de|do|da) ")&&MvpSearchCoordinator.Normalize(text).Contains("incubadora")){item.Type="PetEgg";item.Locations.Clear();item.Jobs.Clear();item.Classes.Clear();}
   else {
    string where=place.Length>0?place:type;string prefix=visual?"Costume_":shadow?"Shadow_":"";
    if(where.Contains("topo"))At(prefix+"Head_Top");if(where.Contains("meio"))At(prefix+"Head_Mid");if(where.Contains("baixo"))At(prefix+"Head_Low");
    if(where.Contains("armadura"))At(prefix+"Armor");if(where.Contains("capa"))At(prefix+"Garment");if(where.Contains("calcad")||where.Contains("sapato")||where=="bota")At(prefix+"Shoes");
    if(where.Contains("escudo"))At(shadow?"Shadow_Shield":"Left_Hand");
    if(where.Contains("acessorio")||where.StartsWith("aces.")){if(!where.Contains("direit"))At(prefix+"Left_Accessory");if(!where.Contains("esquerd"))At(prefix+"Right_Accessory");}
    var weapons=new Dictionary<string,string>{{"espada de duas maos","2hSword"},{"lanca de duas maos","2hSpear"},{"machado de duas maos","2hAxe"},{"cajado de duas maos","2hStaff"},{"arco","Bow"},{"katar","Katar"},{"rifle","Rifle"},{"metralhadora gatling","Gatling"},{"espingarda","Shotgun"},{"lanca-granadas","Grenade"},{"adaga","Dagger"},{"espada","1hSword"},{"lanca","1hSpear"},{"machado","1hAxe"},{"cajado","Staff"},{"livro","Book"},{"maca","Mace"},{"chicote","Whip"},{"instrumento musical","Musical"},{"pistola","Revolver"},{"soqueira","Knuckle"},{"shuriken huuma","Huuma"}};
    if(weapons.TryGetValue(type,out var subtype)){item.Type="Weapon";item.SubType=subtype;At("Right_Hand");}
    else if(type=="carta")item.Type="Card";else if(locations.Count>0)item.Type=shadow?"ShadowGear":"Armor";
    if(shadow&&where.Contains("armamento"))At("Shadow_Weapon");
    if(locations.Count>0)item.Locations=locations;
    if(classes=="todas"){item.Jobs.Clear();item.Classes.Clear();item.Gender="Both";}
    else if(classes.Length>0){var aliases=new Dictionary<string,string>{{"cavaleiro","Knight"},{"templario","Crusader"},{"bruxo","Wizard"},{"sabio","Sage"},{"cacador","Hunter"},{"bardo","Bard"},{"odalisca","Dancer"},{"ferreiro","Blacksmith"},{"alquimista","Alchemist"},{"mercenario","Assassin"},{"arruaceiro","Rogue"},{"sacerdote","Priest"},{"monge","Monk"},{"superaprendiz","SuperNovice"},{"justiceiro","Gunslinger"},{"ninja","Ninja"},{"mestre taekwon","StarGladiator"},{"espiritualista","SoulLinker"}};var jobs=aliases.Where(p=>classes.Contains(p.Key)).Select(p=>p.Value).ToHashSet();foreach(var job in BuildCatalog.Classes)if(classes.Contains(MvpSearchCoordinator.Normalize(job.Name)))jobs.Add(job.Job);foreach(var family in new[]{("espadachim","Swordman"),("mercador","Merchant"),("arqueiro","Archer"),("gatuno","Thief"),("acolito","Acolyte"),("mago","Mage")})if(classes.Contains(family.Item1))foreach(var job in BuildCatalog.Classes.Where(c=>c.Family==family.Item2))jobs.Add(job.Job);if(jobs.Count>0){item.Jobs=jobs.ToDictionary(j=>j,_=>true);item.Classes.Clear();}}
   }
   if(data.Slots>=0)item.Slots=data.Slots;if(item.Type.Length>0)result.Add(item);
  }
  FarmlandLuaImporter.Import(source,database);
  File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
  return $"Farmland: {entries.Count} nomes/descrições; {result.Count} metadados; {result.Count(i=>i.Type=="PetEgg")} ovos.";
 }
}
