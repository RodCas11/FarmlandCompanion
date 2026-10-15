using System.Net;
using System.Text;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class IntegratedSearchTests
{
    sealed class Keys : ICredentialStorageService {
        public bool HasKey {get;private set;}=true;
        public string? Read()=>HasKey?"test-key":null;
        public void Store(string key)=>HasKey=true;
        public void Delete()=>HasKey=false;
    }
    sealed class Handler : HttpMessageHandler {
        public List<string> Paths {get;}=new();
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply {get;set;}=(r,ct)=>Task.FromResult(Json(r.RequestUri!.AbsolutePath.Contains("/mob/")?Mob:Search));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Paths.Add(r.RequestUri!.PathAndQuery);return Reply(r,ct);}
    }
    const string Search="""{"type":"mob","gateway":"iro","items":[{"id":9999,"name":"Online MVP"},{"id":9999,"name":"Duplicate"}],"page":1,"pages":1}""";
    const string Mob="""{"id":9999,"gatewayId":"iro","name":"Online MVP","exp":{"mvp":100},"spawns":[{"map":{"id":"test_map"},"respawn":3600}]}""";
    static HttpResponseMessage Json(string body,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    static MainViewModel Create(Handler h,Keys? keys=null,string? root=null){
        root??=Path.Combine(Path.GetTempPath(),"Farmland-search-"+Guid.NewGuid());var store=new PersistenceService(root);store.Save("ragnaplace.json",new IntegrationSettings{Gateway="iro"});
        return new(store,()=>new DateTimeOffset(new DateTime(2020,1,15,12,0,0),TimeZoneInfo.Local.GetUtcOffset(new DateTime(2020,1,15))),_=>true,new RagnaPlaceApiClient(new HttpClient(h),keys??new(),new LocalCacheService(),TimeSpan.Zero),keys??new());
    }
    static void Dispose(MainViewModel vm){vm.Catalog.Dispose();vm.Spotlight.Dispose();}
    [Theory][InlineData("osíris","Osiris")][InlineData("1039","Baphomet")][InlineData("freeoni","Phreeoni")][InlineData("senhor das trevas","Dark Lord")][InlineData("baph","Baphomet")]
    public void LocalSearchSupportsIdAccentsAliasesAndPartialNames(string query,string expected){var vm=Create(new(),new Keys());try{vm.Catalog.Query=query;Assert.Contains(vm.Catalog.Entries,e=>e.Name==expected);}finally{Dispose(vm);}}
    [Fact] public async Task OnlineOnlyFavoriteDetailsDeathAndCompactSurviveOfflineRestart(){
        var h=new Handler();var vm=Create(h);try{
            var c=vm.Catalog;c.Query="Online";await c.SearchIntegratedAsync(false);
            var row=Assert.Single(c.Entries);Assert.True(row.IsSearchCandidate);Assert.Equal(9999,row.MonsterId);Assert.Empty(c.FavoriteEntries);
            await c.SelectOnlineAsync(row,true);var saved=Assert.Single(c.FavoriteEntries);Assert.False(saved.IsSearchCandidate);Assert.Equal("iro",saved.Gateway);Assert.Single(c.CompactFavorites);Assert.Empty(vm.Engine.Mvps);
            c.SelectedEntry=saved;c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);Assert.True(c.NeedsRespawn);Assert.Empty(vm.Engine.Mvps);
            c.RespawnMin=90;c.RespawnMax=120;c.SaveRequiredRespawnCommand.Execute(null);Assert.Equal(90,Assert.Single(vm.Engine.Mvps).MinMinutes);Assert.Single(c.CompactFavorites);
            var keys=new Keys();keys.Delete();var restart=Create(new(),keys,vm.Store.Root);try{var f=Assert.Single(restart.Catalog.FavoriteEntries);Assert.Equal(9999,f.MonsterId);Assert.Contains("RagnaPlace",f.Origin);Assert.Equal(90,f.CustomMin);Assert.Single(restart.Catalog.CompactFavorites);Assert.NotNull(Assert.Single(restart.Engine.Mvps).DeathUtc);}finally{Dispose(restart);}
        }finally{Dispose(vm);}
    }
    [Fact] public async Task NumericQueryUsesDetailsAndDeduplicatesMaps(){var h=new Handler();var vm=Create(h);try{vm.Catalog.Query="9999";await vm.Catalog.SearchIntegratedAsync(false);Assert.False(Assert.Single(vm.Catalog.Entries).IsSearchCandidate);Assert.Single(h.Paths);Assert.Contains("/mob/9999",h.Paths[0]);}finally{Dispose(vm);}}
    [Fact] public async Task AccentedNameUsesCanonicalDocumentedSearch(){var h=new Handler();using var api=new RagnaPlaceApiClient(new HttpClient(h),new Keys(),new LocalCacheService(),TimeSpan.Zero);await new RagnaPlaceMonsterSearchService(api).SearchAsync("iro","Osíris",1,false,default);Assert.Contains("q=osiris",Assert.Single(h.Paths));}
    [Theory][InlineData("Freeoni","freeoni","phreeoni")][InlineData("Phreeoni","phreeoni","freeoni")][InlineData("Phree","phree","freeoni")]
    public async Task GatewayLanguageTriesTypedNameBeforeAlternative(string query,string first,string fallback){var h=new Handler{Reply=(r,ct)=>Task.FromResult(Json(r.RequestUri!.Query.Contains("q="+first+"&")?"""{"type":"mob","items":[]}""":Search))};using var api=new RagnaPlaceApiClient(new HttpClient(h),new Keys(),new LocalCacheService(),TimeSpan.Zero);var rows=await new RagnaPlaceMonsterSearchService(api).SearchAsync("iro",query,1,false,default);Assert.Single(rows);Assert.Equal(2,h.Paths.Count);Assert.Contains("q="+first,h.Paths[0]);Assert.Contains("q="+fallback,h.Paths[1]);}
    [Fact] public async Task MissingConfigurationDoesNotSendAnyNetworkRequest(){var h=new Handler();var keys=new Keys();keys.Delete();var vm=Create(h,keys);try{vm.Catalog.Query="Baphomet";await vm.Catalog.SearchIntegratedAsync(false);Assert.Empty(h.Paths);Assert.Contains("Busca local",vm.Catalog.OnlineStatus);Assert.NotEmpty(vm.Catalog.Entries);}finally{Dispose(vm);}}
    [Fact] public async Task InvalidGatewayDoesNotSendAnyNetworkRequest(){var h=new Handler();var vm=Create(h);try{vm.Catalog.GatewaySlug="../bad";vm.Catalog.Query="Baphomet";await vm.Catalog.SearchIntegratedAsync(false);Assert.Empty(h.Paths);Assert.Contains("gateway",vm.Catalog.OnlineStatus);}finally{Dispose(vm);}}
    [Fact] public async Task ConnectionFailurePreservesSavedResults(){var vm=Create(new Handler{Reply=(r,ct)=>throw new HttpRequestException()});try{vm.Catalog.Query="Baphomet";await vm.Catalog.SearchIntegratedAsync(false);Assert.Contains("Sem conexão",vm.Catalog.OnlineStatus);Assert.NotEmpty(vm.Catalog.Entries);}finally{Dispose(vm);}}
    [Fact] public async Task DebounceAndCancellationOnlyPublishLatestQuery(){var h=new Handler();using var api=new RagnaPlaceApiClient(new HttpClient(h),new Keys(),new LocalCacheService(),TimeSpan.Zero);using var coordinator=new MvpSearchCoordinator(api);var first=coordinator.SearchAsync("iro","old");var second=coordinator.SearchAsync("iro","new");Assert.Null(await first);Assert.Single((await second)!);Assert.Contains("q=new",Assert.Single(h.Paths));}
    [Fact] public async Task InFlightCancellationDoesNotLeakOldResults(){var started=new TaskCompletionSource();var h=new Handler{Reply=async(r,ct)=>{started.TrySetResult();await Task.Delay(Timeout.Infinite,ct);return Json(Search);}};using var api=new RagnaPlaceApiClient(new HttpClient(h),new Keys(),new LocalCacheService(),TimeSpan.Zero);using var coordinator=new MvpSearchCoordinator(api);var task=coordinator.SearchAsync("iro","old",debounce:false);await started.Task;coordinator.Cancel();Assert.Null(await task);}
    [Theory][InlineData(401)][InlineData(403)][InlineData(404)][InlineData(429)]
    public async Task FailuresKeepLocalResultsAndExplainError(int code){var h=new Handler{Reply=(r,ct)=>Task.FromResult(Json("secret test-key",(HttpStatusCode)code))};var vm=Create(h);try{var c=vm.Catalog;c.Query="Baphomet";await c.SearchIntegratedAsync(false);Assert.Contains(c.Entries,e=>e.Name=="Baphomet");Assert.Contains("Não foi possível consultar",c.OnlineStatus);Assert.DoesNotContain("test-key",c.OnlineStatus);Assert.Contains(code.ToString(),c.AuthStatus);}finally{Dispose(vm);}}
    [Theory][InlineData("{}")] [InlineData("not-json")]
    public async Task UnexpectedJsonIsNotAnEmptySearch(string body){var vm=Create(new Handler{Reply=(r,ct)=>Task.FromResult(Json(body))});try{vm.Catalog.Query="unknown";await vm.Catalog.SearchIntegratedAsync(false);Assert.Contains("formato inesperado",vm.Catalog.OnlineStatus);}finally{Dispose(vm);}}
    [Fact] public async Task TimeoutHasFailureMessageInsteadOfNoMatches(){var vm=Create(new Handler{Reply=(r,ct)=>throw new TaskCanceledException()});try{vm.Catalog.Query="unknown";await vm.Catalog.SearchIntegratedAsync(false);Assert.Contains("tempo",vm.Catalog.OnlineStatus);Assert.DoesNotContain("Nenhum MVP",vm.Catalog.OnlineStatus);}finally{Dispose(vm);}}
    [Fact] public async Task EmptyResultsAreDistinctFromFailure(){var vm=Create(new Handler{Reply=(r,ct)=>Task.FromResult(Json("""{"type":"mob","items":[]}"""))});try{vm.Catalog.Query="unknown";await vm.Catalog.SearchIntegratedAsync(false);Assert.Contains("Nenhum MVP encontrado",vm.Catalog.OnlineStatus);}finally{Dispose(vm);}}
    [Fact] public async Task BossWithoutMvpExperienceCannotBeFavorited(){var h=new Handler{Reply=(r,ct)=>Task.FromResult(Json(r.RequestUri!.AbsolutePath.Contains("/mob/")?Mob.Replace("\"mvp\":100","\"mvp\":0"):Search))};var vm=Create(h);try{vm.Catalog.Query="Online";await vm.Catalog.SearchIntegratedAsync(false);await vm.Catalog.SelectOnlineAsync(Assert.Single(vm.Catalog.Entries),true);Assert.Empty(vm.Catalog.FavoriteEntries);Assert.Contains("não confirma",vm.Catalog.OnlineStatus);}finally{Dispose(vm);}}
    [Fact] public async Task RefreshNeverOverwritesFarmlandConfiguration(){var h=new Handler();var vm=Create(h);try{var c=vm.Catalog;c.Query="9999";await c.SearchIntegratedAsync(false);c.SelectedEntry=c.Entries.Single();c.RespawnMin=90;c.RespawnMax=120;c.ConfirmFarmland=true;c.SaveRespawnCommand.Execute(null);await c.SearchIntegratedAsync(false,true);var row=Assert.Single(c.Entries);Assert.Equal(90,row.CustomMin);Assert.Equal(120,row.CustomMax);Assert.True(row.FarmlandConfirmed);}finally{Dispose(vm);}}
}
