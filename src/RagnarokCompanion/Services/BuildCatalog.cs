using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using RagnarokCompanion.Models;
using YamlDotNet.Serialization;
namespace RagnarokCompanion.Services;
public static class BuildCatalog
{
    public static readonly BuildClass[] Classes={
        new("lord","Lorde","Knight","Swordman","Lord_Knight.png"),new("paladin","Paladino","Crusader","Swordman","Paladin.png"),
        new("high-wizard","Arquimago","Wizard","Mage","High_Wizard.png"),new("professor","Professor","Sage","Mage","Scholar.png"),
        new("sniper","Atirador de Elite","Hunter","Archer","Sniper.png"),new("minstrel","Menestrel","Bard","Archer","Minstrel.png",true,"Male"),new("gypsy","Cigana","Dancer","Archer","Gypsy.png",true,"Female"),
        new("whitesmith","Mestre-Ferreiro","Blacksmith","Merchant","Mastersmith.png"),new("creator","Criador","Alchemist","Merchant","Biochemist.png"),
        new("assassin-cross","Algoz","Assassin","Thief","Assassin_Cross.png"),new("stalker","Desordeiro","Rogue","Thief","Stalker.png"),
        new("high-priest","Sumo Sacerdote","Priest","Acolyte","High_Priest.png"),new("champion","Mestre","Monk","Acolyte","Champion.png"),
        new("super-novice","Superaprendiz","SuperNovice","Novice","Super_Novice.png",false),new("gunslinger","Justiceiro","Gunslinger","Gunslinger","Gunslinger.png",false),new("ninja","Ninja","Ninja","Ninja","Ninja.png",false),
        new("star-gladiator","Mestre Taekwon","StarGladiator","Taekwon","TaeKwon_Master.png",false),new("soul-linker","Espiritualista","SoulLinker","Taekwon","Soul_Linker.png",false)
    };
    public static readonly string[] Stages={"Early","Mid","End"},Purposes={"PvM","PvP","MVP","Farm","Suporte"},Grades={"Sem","D","C","B","A"};
    public static readonly BuildSlot[] Slots={
        new("head-top","Topo","Normal","Head_Top"),new("head-mid","Meio","Normal","Head_Mid"),new("head-low","Baixo","Normal","Head_Low"),new("weapon","Arma","Normal","Right_Hand"),new("armor","Armadura","Normal","Armor"),new("shield","Escudo","Normal","Left_Hand"),new("garment","Capa","Normal","Garment"),new("shoes","Calçados","Normal","Shoes"),new("accessory-left","Acessório esquerdo","Normal","Left_Accessory"),new("accessory-right","Acessório direito","Normal","Right_Accessory"),
        new("costume-top","Visual topo","Visual","Costume_Head_Top"),new("costume-mid","Visual meio","Visual","Costume_Head_Mid"),new("costume-low","Visual baixo","Visual","Costume_Head_Low"),new("costume-garment","Visual capa","Visual","Costume_Garment"),
        new("pet-egg","Ovo de Mascote","Pet","PetEgg"),
        new("shadow-weapon","Shadow arma","Shadow","Shadow_Weapon"),new("shadow-armor","Shadow armadura","Shadow","Shadow_Armor"),new("shadow-shield","Shadow escudo","Shadow","Shadow_Shield"),new("shadow-shoes","Shadow calçados","Shadow","Shadow_Shoes"),new("shadow-left","Shadow esquerdo","Shadow","Shadow_Left_Accessory"),new("shadow-right","Shadow direito","Shadow","Shadow_Right_Accessory")
    };
    public static int Total=>Classes.Length*Stages.Length;
    public static PlayerBuild New(){var build=new PlayerBuild();Normalize(build);return build;}
    public static void Normalize(PlayerBuild build)=>BuildRecovery.Normalize(build);
    public static string Combination(PlayerBuild b)=>b.ClassId+":"+b.Stage;
}
public sealed class BuildEquipmentCatalog
{
    readonly Dictionary<int,BuildItemMetadata> items;
    public BuildEquipmentCatalog(IEnumerable<BuildItemMetadata>? source=null)
    {
        if(source!=null){items=source.ToDictionary(i=>i.Id);return;}
        using var stream=typeof(BuildEquipmentCatalog).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.build-equipment.json.gz");
        if(stream==null){items=new();return;}using var gz=new GZipStream(stream,CompressionMode.Decompress);items=(JsonSerializer.Deserialize<List<BuildItemMetadata>>(gz)??new()).ToDictionary(i=>i.Id);
    }
    // Farmland item descriptions explicitly specify Meio, Todas and one socket.
    static readonly Dictionary<int,BuildItemMetadata> farmland=LoadFarmland(); static Dictionary<int,BuildItemMetadata> LoadFarmland(){using var stream=typeof(BuildEquipmentCatalog).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.build-farmland-items.json");return stream==null?LegacyFarmland:(JsonSerializer.Deserialize<List<BuildItemMetadata>>(stream)??new()).ToDictionary(i=>i.Id);} static Dictionary<int,BuildItemMetadata> LegacyFarmland=>new(){
        [19456]=new(){Id=19456,Type="Armor",Slots=1,Locations=new(){{"Head_Mid",true}}},
        [19457]=new(){Id=19457,Type="Armor",Slots=1,Locations=new(){{"Head_Mid",true}}}
    };
    static readonly Dictionary<int,BuildItemMetadata> pets=LoadPets();
    static Dictionary<int,BuildItemMetadata> LoadPets(){using var stream=typeof(BuildEquipmentCatalog).Assembly.GetManifestResourceStream("RagnarokCompanion.Data.build-pet-eggs.json");return stream==null?new():(JsonSerializer.Deserialize<List<BuildItemMetadata>>(stream)??new()).ToDictionary(i=>i.Id);}
    public BuildItemMetadata? Find(int id)=>farmland.GetValueOrDefault(id)??items.GetValueOrDefault(id)??pets.GetValueOrDefault(id);
    static bool Flag(Dictionary<string,bool> map,string key)=>map.GetValueOrDefault(key);
    public static bool At(BuildItemMetadata item,string location)=>Flag(item.Locations,location)||location=="Right_Hand"&&Flag(item.Locations,"Both_Hand")||location is "Left_Accessory" or "Right_Accessory"&&Flag(item.Locations,"Both_Accessory");
    public bool Compatible(BuildItemMetadata item,BuildClass job,BuildSlot slot)
    {
        if(slot.Group=="Pet")return item.Type.Equals("PetEgg",StringComparison.OrdinalIgnoreCase);
        bool jobs=item.Jobs.Count==0||Flag(item.Jobs,"All")||Flag(item.Jobs,job.Job);
        if(item.Jobs.TryGetValue(job.Job,out bool explicitJob)&&!explicitJob)jobs=false;
        bool classes=item.Classes.Count==0||Flag(item.Classes,"All")||Flag(item.Classes,job.Upper?"Upper":"Normal");
        if(item.Classes.TryGetValue(job.Upper?"Upper":"Normal",out bool explicitClass)&&!explicitClass)classes=false;
        bool gender=item.Gender=="Both"||job.Gender=="Both"||item.Gender==job.Gender;
        return jobs&&classes&&gender&&At(item,slot.Location)&&item.Type is "Armor" or "Weapon" or "ShadowGear";
    }
    public static bool TwoHanded(BuildItemMetadata? item)=>item!=null&&new[]{"2hSword","2hSpear","2hAxe","2hStaff","Bow","Katar","Rifle","Gatling","Shotgun","Grenade"}.Contains(item.SubType);
    public static int Import(string equipmentYaml,string etcYaml,string output)
    {
        var parser=new DeserializerBuilder().IgnoreUnmatchedProperties().Build();var items=new List<BuildItemMetadata>();
        foreach(var path in new[]{equipmentYaml,etcYaml}){using var reader=File.OpenText(path);var data=parser.Deserialize<FarmlandItemImporter.Document<BuildItemMetadata>>(reader);items.AddRange(data.Body.Where(i=>i.Type is "Armor" or "Weapon" or "ShadowGear" or "Card" or "Petegg" or "PetEgg"));}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);using var file=File.Create(output);using var gz=new GZipStream(file,CompressionLevel.SmallestSize);JsonSerializer.Serialize(gz,items.GroupBy(i=>i.Id).Select(g=>g.First()).ToArray());return items.Count;
    }
}
