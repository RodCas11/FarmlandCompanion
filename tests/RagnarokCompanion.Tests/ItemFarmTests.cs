using System.Net;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class ItemFarmTests {
 [Fact] public void PortugueseEnglishAndPublicMapsPreserveFarmlandDrops(){var index=ItemFarmIndex.Shared;var apron=index.Find(661)!;Assert.Equal("[Q] Avental Elegante",apron.Name);Assert.Equal("Avental Elegante",apron.UnidentifiedName);Assert.Contains(index.Search(apron.EnglishName),i=>i.Id==661);Assert.Contains(index.Search("avental elegante"),i=>i.Id==661);Assert.Equal("Areia Estelar de Bruxa",index.Find(1061)!.Name);Assert.Contains(index.Search("areia estelar"),i=>i.Id==1061);var rows=index.Rank(1061,10,true,false);Assert.Contains(rows,r=>r.Monster.Id==1102);Assert.Contains(rows,r=>r.Monster.Id==1378);Assert.DoesNotContain(rows,r=>r.Monster.Id==2885);var bathory=rows.Single(r=>r.Monster.Id==1102);Assert.Equal("alde_dun04",bathory.BestMap!.Map);Assert.Contains("Renewal",bathory.MapSource);var aliza=index.Rank(661,0,true,false).Single(r=>r.Monster.Id==1737);Assert.Equal(0.01,aliza.Chance);Assert.Equal("kh_dun01",aliza.BestMap!.Map);Assert.Equal(55,aliza.BestMap.Count);Assert.DoesNotContain(aliza.Monster.Spawns,s=>s.Map=="kh_kiehl01");Assert.All(rows,r=>Assert.InRange(r.Chance,10,100));}
 [Fact] public void LuaIsDecodedAsDataAndKeepsIdentifiedPrefixesAndUnknownAliases(){System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".lua");File.WriteAllBytes(file,System.Text.Encoding.GetEncoding(1252).GetBytes("""
-- [999] = { identifiedDisplayName = "not an item" }
tbl = { [661] = { unidentifiedDisplayName = "Avental Elegante", identifiedDisplayName = "[Q] Avental Elegante", descriptions = { "braces } { are data" } }, [1061] = {identifiedDisplayName = "Areia Estelar de Bruxa", unidentifiedDisplayName = "Areia Estrelar de Bruxa"}, [501] = {identifiedDisplayName = "Poção Vermelha"} }
os.execute("DO_NOT_EXECUTE")
"""));var names=LuaItemNameReader.Read(file);Assert.Equal(3,names.Count);Assert.Equal("[Q] Avental Elegante",names[661].Identified);Assert.Equal("Avental Elegante",names[661].Unidentified);Assert.Equal("Poção Vermelha",names[501].Identified);}
 [Fact] public void SpecialVariantsAreExcludedEvenWithRegularSpawnsAndUnknownMapsRemainHonest(){var index=new ItemFarmIndex(new(){Items=new(){new(){Id=1,Name="Test"}},Monsters=new(){new(){Id=1,Name="Furious Bathory",Spawns=new(){new(){Map="field",Count=100,Evidence="confirmed"}}},new(){Id=2,Name="Bathory",Spawns=new(){new(){Map="field",Name="Field",Count=20,Evidence="confirmed"}}},new(){Id=3,Name="Demon Pungus"},new(){Id=4,Name="Instance",IsSpecial=true}},Drops=new(){new(){ItemId=1,MonsterId=1,Chance=100},new(){ItemId=1,MonsterId=2,Chance=25},new(){ItemId=1,MonsterId=3,Chance=50},new(){ItemId=1,MonsterId=4,Chance=100}}});var rows=index.Rank(1,0,true,false);Assert.Equal(new[]{3,2},rows.Select(r=>r.Monster.Id));Assert.Equal("Mapa não identificado",rows[0].MapLabel);Assert.Null(rows[0].Score);Assert.Equal(2,index.Rank(1,0,true,true)[0].Monster.Id);Assert.False(index.Rank(1,0,false,false).Single(r=>r.Monster.Id==1).Farmable);var rules=new ItemMonsterRules{IncludeIds=new(){1}};Assert.False(rules.Excluded(1,"Furious Bathory",classified:true));}
 [Fact] public void ImportProjectsOnlyAuthorizedDataAndPreservesDatabaseOnFailure(){var root=Path.Combine(Path.GetTempPath(),"farmland-import-"+Guid.NewGuid());Directory.CreateDirectory(root);var yaml="""
Header:
  Type: ITEM_DB
Body:
  - Id: 1061
    AegisName: PRIVATE_JOIN_NAME
    Name: Public Item
    Script: |
      CONFIDENTIAL_CANARY_SCRIPT;
    Confidential: PRIVATE_ATTRIBUTE
""";File.WriteAllText(Path.Combine(root,"item_db_etc.yml"),yaml);foreach(var file in new[]{"item_db_equip.yml","item_db_usable.yml"})File.WriteAllText(Path.Combine(root,file),"Body: []");File.WriteAllText(Path.Combine(root,"mob_db.yml"),"""
Body:
  - Id: 1
    AegisName: BATHORY
    Name: Bathory
    Class: Normal
    Hp: 987654321
    Drops:
      - Item: PRIVATE_JOIN_NAME
        Rate: 2425
  - Id: 2
    AegisName: C5_BATHORY
    Name: Disguised variant
    Class: Normal
    Drops:
      - Item: PRIVATE_JOIN_NAME
        Rate: 10000
""");var database=Path.Combine(root,"public.sqlite");FarmlandItemImporter.Import(root,database);var index=new ItemFarmIndex(database);Assert.Equal(24.25,Assert.Single(index.Rank(1061,0,true,false)).Chance);Assert.Equal(100,index.Rank(1061,0,false,false).Single(r=>r.Monster.Id==2).Chance);var bytes=File.ReadAllBytes(database);var text=System.Text.Encoding.UTF8.GetString(bytes);foreach(var secret in new[]{"CONFIDENTIAL_CANARY_SCRIPT","PRIVATE_ATTRIBUTE","PRIVATE_JOIN_NAME","987654321"})Assert.DoesNotContain(secret,text);Assert.Equal(yaml,File.ReadAllText(Path.Combine(root,"item_db_etc.yml")));File.WriteAllText(Path.Combine(root,"mob_db.yml"),"invalid: [");Assert.ThrowsAny<Exception>(()=>FarmlandItemImporter.Import(root,database));Assert.Equal(bytes,File.ReadAllBytes(database));FarmlandItemImporter.ImportSpawns(database,new[]{new FarmlandItemImporter.PublicSpawn{MonsterId=1,MapCode="field",MapName="Field",Count=10,Regular=true},new FarmlandItemImporter.PublicSpawn{MonsterId=1,MapCode="field",MapName="Field",Count=20,Regular=true}});var ranked=Assert.Single(new ItemFarmIndex(database).Rank(1061,0,true,false));Assert.Equal(30,ranked.BestMap!.Count);Assert.True(ranked.HasMap);}
 sealed class Keys:ICredentialStorageService{public bool HasKey=>true;public string Read()=>"TEST-ITEM-KEY";public void Store(string key){}public void Delete(){}}
 sealed class Handler:HttpMessageHandler {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Assert.Equal("/api/database/Item/1061",request.RequestUri!.AbsolutePath);Assert.Equal("pt",request.Headers.GetValues("Accept-Language").Single());Assert.Equal("LATAM",request.Headers.GetValues("x-server").Single());return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("""{"id":1061,"name":"Areia Estelar de Bruxa","description":"Referência de item","type":"Etc","iconUrl":"https://static.divine-pride.net/images/items/item/1061.png"}""")});}}
 [Fact] public async Task ItemLookupReusesCredentialsHeadersAndPersistentCache(){var store=new PersistenceService(Path.Combine(Path.GetTempPath(),"item-test-"+Guid.NewGuid()));var handler=new Handler();using(var api=new DivinePrideApiClient(new HttpClient(handler),new Keys(),store)){var item=await api.ItemAsync("LATAM",1061);Assert.Equal("Areia Estelar de Bruxa",item.Name);Assert.NotNull(item.IconUrl);await api.ItemAsync("LATAM",1061);Assert.Equal(1,handler.Calls);}using var offline=new DivinePrideApiClient(new HttpClient(handler),new Keys(),store);Assert.Equal("Referência de item",(await offline.ItemAsync("LATAM",1061)).Description);Assert.Equal(1,handler.Calls);Assert.DoesNotContain("TEST-ITEM-KEY",File.ReadAllText(Path.Combine(store.Root,"divine-pride-cache.json")));}
}
