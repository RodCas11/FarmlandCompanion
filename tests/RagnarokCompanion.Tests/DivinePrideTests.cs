using System.Net;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class DivinePrideTests
{
 sealed class Keys:ICredentialStorageService{public bool HasKey=>true;public string Read()=>"TEST-KEY";public void Store(string key){}public void Delete(){}}
 sealed class Handler:HttpMessageHandler{
  public int Calls;public HttpStatusCode Code=HttpStatusCode.OK;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;Assert.Equal("https://www.divine-pride.net/api/database/Monster/1832?apiKey=TEST-KEY",r.RequestUri!.AbsoluteUri);Assert.Equal("pt",r.Headers.GetValues("Accept-Language").Single());Assert.Equal("LATAM",r.Headers.GetValues("x-server").Single());return Task.FromResult(new HttpResponseMessage(Code){Content=new StringContent(Code==HttpStatusCode.OK?"""{"id":1832,"name":"Ifrit","type":"MVP","level":146,"spawns":[{"respawn":999999}]}""":"Do not expose TEST-KEY")});}
 }
 static PersistenceService Store()=>new(Path.Combine(Path.GetTempPath(),"divine-test-"+Guid.NewGuid()));
 [Fact] public async Task DocumentedHeadersPersistentCacheAndUnknownSpawnUnits(){var store=Store();var handler=new Handler();using(var client=new DivinePrideApiClient(new HttpClient(handler),new Keys(),store)){var m=await client.MonsterAsync("LATAM",1832);Assert.Equal("Ifrit",m.Name);Assert.Null(m.Spawns);await client.MonsterAsync("LATAM",1832);Assert.Equal(1,handler.Calls);}var noNetwork=new Handler{Code=HttpStatusCode.ServiceUnavailable};using var offline=new DivinePrideApiClient(new HttpClient(noNetwork),new Keys(),store);Assert.Equal("Ifrit",(await offline.MonsterAsync("LATAM",1832)).Name);Assert.Equal(0,noNetwork.Calls);Assert.DoesNotContain("TEST-KEY",File.ReadAllText(Path.Combine(store.Root,"divine-pride-cache.json")));}
 [Theory][InlineData(401)][InlineData(429)] public async Task ErrorsNeverExposeRequestUrlOrKey(int code){var handler=new Handler{Code=(HttpStatusCode)code};using var client=new DivinePrideApiClient(new HttpClient(handler),new Keys());var ex=await Assert.ThrowsAsync<DivinePrideException>(()=>client.MonsterAsync("LATAM",1832));Assert.DoesNotContain("TEST-KEY",ex.ToString());Assert.DoesNotContain("apiKey=",ex.ToString());if(code==429){await Assert.ThrowsAsync<DivinePrideException>(()=>client.MonsterAsync("LATAM",1832));Assert.Equal(1,handler.Calls);}}
 [Fact] public void NewCredentialStoreNeverUsesOrDeletesOldProviderKey(){var store=Store();var old=Path.Combine(store.Root,"ragnaplace-key.dpapi");File.WriteAllText(old,"legacy untouched");var keys=new CredentialStorageService(store.Root);Assert.False(keys.HasKey);keys.Store("TEST-NEW-KEY");Assert.Equal("TEST-NEW-KEY",keys.Read());Assert.DoesNotContain("TEST-NEW-KEY",System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(store.Root,"divine-pride-key.dpapi"))));keys.Delete();Assert.Equal("legacy untouched",File.ReadAllText(old));}
 [Fact] public void LegacyFavoritesTimersAndLocalNameSearchRemainIntact(){var store=Store();store.Save("catalog-choices.json",new[]{new CatalogEntry{Key="api:old:1832:thor_v03",MonsterId=1832,Name="Ifrit",Map="thor_v03",Gateway="laro-pt",Origin="RagnaPlace",Favorite=true,CustomMin=90,CustomMax=100}});using var vm=new VmScope(new MainViewModel(store));vm.Model.Catalog.Query="ifrit";Assert.NotEmpty(vm.Model.Catalog.Entries);var old=Assert.Single(vm.Model.Catalog.FavoriteEntries);Assert.Equal("api:old:1832:thor_v03",old.Key);Assert.Equal(90,new MvpRespawnService().Resolve(old,false)!.Value.Min);Assert.Single(vm.Model.Catalog.CompactFavorites);Assert.Equal("LATAM",vm.Model.Catalog.GatewaySlug);Assert.Equal("pt",vm.Model.Catalog.Language);}
 sealed class VmScope:IDisposable{public MainViewModel Model;public VmScope(MainViewModel model)=>Model=model;public void Dispose(){Model.Catalog.Dispose();Model.Spotlight.Dispose();}}
}
