using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RagnarokCompanion.Models;
using RagnarokCompanion.Models.Api;
using RagnarokCompanion.Services;
using Xunit;

namespace RagnarokCompanion.Tests;
public sealed class RagnaPlaceTests
{
    private const string SearchJson = """{"type":"mob","gateway":"iro","page":1,"pages":1,"items":[{"id":1159,"name":"Phreeoni"}]}""";
    private const string MobJson = """{"id":1159,"gatewayId":"iro","name":"Phreeoni","exp":{"mvp":123},"spawns":[{"map":{"id":"moc_fild17"},"respawn":3600},{"map":{"id":"moc_fild19"},"respawn":7200}]}""";
    private sealed class FakeCredentials : ICredentialStorageService
    {
        public bool HasKey => Key != null;
        private string? Key = "dummy-test-key";
        public string? Read() => Key;
        public void Store(string key) => Key = key;
        public void Delete() => Key = null;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Responder = _ => Task.FromResult(Response(HttpStatusCode.OK, SearchJson));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Calls++; return Responder(r); }
    }
    private static HttpResponseMessage Response(HttpStatusCode code, string content = "{}") => new(code) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private static RagnaPlaceApiClient Client(Handler h, FakeCredentials? keys = null) => new(new HttpClient(h), keys ?? new(), new LocalCacheService(), TimeSpan.Zero);
    [Fact] public async Task AuthenticationUsesHeaderAndRealDocumentedSearchRoute()
    {
        var h = new Handler(); h.Responder = r =>
        {
            Assert.Equal("https://api.ragnaplace.com/v1/iro/search?type=mob&q=Poring&page=1&limit=10", r.RequestUri!.AbsoluteUri);
            Assert.Equal("dummy-test-key", r.Headers.GetValues("x-api-key").Single());
            Assert.DoesNotContain("dummy", r.RequestUri.AbsoluteUri); return Task.FromResult(Response(HttpStatusCode.OK, SearchJson));
        };
        using var c = Client(h); await new RagnaPlaceAuthService(c).TestAsync("iro"); Assert.Equal(1, h.Calls);
    }
    [Theory] [InlineData(401)] [InlineData(403)] public async Task UnauthorizedIsSafeAndNeverRetried(int code)
    {
        var h = new Handler { Responder = _ => Task.FromResult(Response((HttpStatusCode)code, "secret server body dummy-test-key")) };
        using var c = Client(h); var ex = await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring"));
        Assert.Equal((HttpStatusCode)code, ex.Status); Assert.DoesNotContain("dummy-test-key", ex.ToString()); Assert.Equal(1, h.Calls);
    }
    [Fact] public async Task MissingKeyNeverSendsRequest()
    {
        var h = new Handler(); var keys = new FakeCredentials(); keys.Delete(); using var c = Client(h, keys);
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring")); Assert.Equal(0, h.Calls);
    }
    [Fact] public async Task RateLimitHonorsRetryAfterWithoutRetryingOrSendingAnotherRequest()
    {
        var h = new Handler { Responder = _ => { var r = Response(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90)); return Task.FromResult(r); } };
        using var c = Client(h);
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring"));
        var ex = await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Maya")); Assert.Equal(HttpStatusCode.TooManyRequests, ex.Status); Assert.Equal(1, h.Calls);
    }
    [Fact] public async Task SuccessfulResponseWithExhaustedQuotaPausesNextNetworkRequest()
    {
        var h = new Handler { Responder = _ => { var r = Response(HttpStatusCode.OK, SearchJson); r.Headers.Add("X-RateLimit-Remaining", "0"); r.Headers.Add("X-RateLimit-Reset", "40"); return Task.FromResult(r); } };
        using var c = Client(h); await c.SearchAsync("iro", "Poring"); await c.SearchAsync("iro", "Poring");
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Maya")); Assert.Equal(1, h.Calls);
    }
    [Theory] [InlineData("not json")] [InlineData("{}")] [InlineData("null")] [InlineData("{\"type\":\"mob\",\"items\":[null]}")]
    public async Task InvalidResponsesAreRejectedAndNotRetained(string body)
    {
        var h = new Handler { Responder = _ => Task.FromResult(Response(HttpStatusCode.OK, body)) }; using var c = Client(h);
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring"));
        h.Responder = _ => Task.FromResult(Response(HttpStatusCode.OK, SearchJson)); await c.SearchAsync("iro", "Poring"); Assert.Equal(2, h.Calls);
    }
    [Fact] public async Task ConcurrentIdenticalQueriesCoalesceAndRefreshIsExplicit()
    {
        var h = new Handler { Responder = async _ => { await Task.Delay(15); return Response(HttpStatusCode.OK, SearchJson); } };
        using var c = Client(h); await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => c.SearchAsync("iro", "Poring"))); Assert.Equal(1, h.Calls);
        await c.SearchAsync("iro", "Poring", refresh: true); Assert.Equal(2, h.Calls);
        c.ClearCache(); await c.SearchAsync("iro", "Poring"); Assert.Equal(3, h.Calls);
    }
    [Fact] public async Task GatewayDiscoveryIsUnauthenticatedAndOnlyReturnsActiveRenewal()
    {
        var h = new Handler { Responder = r =>
        {
            Assert.False(r.Headers.Contains("x-api-key"));
            return Task.FromResult(Response(HttpStatusCode.OK, """{"gateways":[{"slug":"iro","group":["renewal"]},{"slug":"bro","group":["renewal","dead"]},{"slug":"roztw","group":["zero"]}]}"""));
        } };
        using var c = Client(h); Assert.Equal("iro", Assert.Single(await c.GatewaysAsync()).Slug);
    }
    [Fact] public async Task MobDetailsConvertSecondsAndKeepMapsSeparate()
    {
        var h = new Handler { Responder = r => { Assert.EndsWith("/v1/iro/mob/1159", r.RequestUri!.AbsoluteUri); return Task.FromResult(Response(HttpStatusCode.OK, MobJson)); } };
        using var c = Client(h); var entries = await new RagnaPlaceCatalogProvider(c).DetailsAsync("iro", 1159);
        Assert.Equal(2, entries.Count); Assert.Equal(60, entries[0].MinMinutes); Assert.Equal(120, entries[1].MaxMinutes); Assert.NotEqual(entries[0].Key, entries[1].Key);
        Assert.All(entries, e => Assert.Contains("RagnaPlace", e.Origin));
    }
    [Fact] public async Task BossClassAloneDoesNotInventMvpClassification()
    {
        var h = new Handler { Responder = _ => Task.FromResult(Response(HttpStatusCode.OK, """{"id":1159,"name":"Boss","class":"Boss","exp":{"mvp":0}}""")) };
        using var c = Client(h); await Assert.ThrowsAsync<RagnaPlaceException>(() => new RagnaPlaceCatalogProvider(c).DetailsAsync("iro", 1159));
    }
    [Fact] public async Task NetworkFailureDoesNotAffectMiningOrMvpTimers()
    {
        var h = new Handler { Responder = _ => throw new HttpRequestException("dummy-test-key must never leak") }; using var c = Client(h);
        var start = DateTimeOffset.UtcNow; var e = new TimerService(new(), new(), new(), new()); e.StartMining(start); var m = new Mvp(); e.Mvps.Add(m); e.Kill(m, start);
        var ex = await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring")); Assert.DoesNotContain("dummy-test-key", ex.ToString());
        Assert.Equal(MiningPhase.Cooldown, e.Phase(start.AddMinutes(31))); Assert.Equal(start.AddMinutes(60), m.StartUtc); Assert.Equal(start, e.Mining.EnteredUtc);
    }
    [Fact] public async Task ServiceUnavailableHasOnlyOneBoundedRetry()
    {
        var h = new Handler { Responder = _ => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable)) }; using var c = Client(h);
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring")); Assert.Equal(2, h.Calls);
    }
    [Fact] public async Task TimeoutIsFriendlyAndDoesNotExposeRawException()
    {
        var h = new Handler { Responder = _ => throw new TaskCanceledException("dummy-test-key") }; using var c = Client(h);
        var ex = await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring")); Assert.DoesNotContain("dummy-test-key", ex.ToString());
    }
    [Fact] public async Task QueryValidationPreventsUnboundedPaginationAndInjection()
    {
        var h = new Handler(); using var c = Client(h);
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("../iro", "Poring"));
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring", 21)); Assert.Equal(0, h.Calls);
    }
    [Fact] public void FarmlandOverridesBeatGatewayValuesAndRequireNoNetwork()
    {
        var e = new CatalogEntry { MinMinutes = 60, MaxMinutes = 60, CustomMin = 120, CustomMax = 180, FarmlandConfirmed = true };
        var service = new MvpRespawnService(); var r = service.Resolve(e, true)!.Value;
        Assert.Equal(120, r.Min); Assert.Equal(180, r.Max); Assert.Contains("Farmland", r.Origin);
        e.CustomMin = e.CustomMax = null; Assert.Null(service.Resolve(e, false)); Assert.Equal(60, service.Resolve(e, true)!.Value.Min);
    }
    [Fact] public void DpapiProtectsKeyAndItIsNeverIncludedInJsonSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-dpapi-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            const string dummy = "NOT-A-REAL-API-KEY-secure-test"; var s = new CredentialStorageService(root); s.Store(dummy);
            Assert.DoesNotContain(dummy, Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "ragnaplace-key.dpapi")))); Assert.Equal(dummy, s.Read());
            var fresh = new CredentialStorageService(root); Assert.Equal(dummy, fresh.Read());
            Assert.DoesNotContain(dummy, JsonSerializer.Serialize(new IntegrationSettings())); Assert.DoesNotContain(dummy, JsonSerializer.Serialize(new Settings()));
            s.Delete(); Assert.False(s.HasKey); Assert.Null(s.Read());
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void OfflineCatalogFavoritesAndCustomRespawnSurviveReloadAndApiMerge()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-catalog-" + Guid.NewGuid());
        try
        {
            var store = new PersistenceService(root); var s = new MvpCatalogService(store); Assert.Equal(16, s.Entries.Count);
            var e = s.Entries[0]; e.Favorite = true; e.CustomMin = 123; e.CustomMax = 150; e.FarmlandConfirmed = true; s.SaveChoices();
            var reloaded = new MvpCatalogService(store); var f = Assert.Single(reloaded.Entries, e => e.Favorite); Assert.Equal(123, f.CustomMin);
            reloaded.Merge(new[] { new CatalogEntry { Key = f.Key, MonsterId = f.MonsterId, Name = f.Name, MinMinutes = 10, MaxMinutes = 10 } });
            var merged = reloaded.Entries.Single(e => e.Key == f.Key); Assert.Equal(123, merged.CustomMin); Assert.True(merged.FarmlandConfirmed);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void SessionCacheExpiresAndNeverCreatesFiles()
    {
        var c = new LocalCacheService(); c.Set("expired", new object(), TimeSpan.FromSeconds(-1)); Assert.False(c.TryGet<object>("expired", out _));
        var value = new object(); c.Set("ok", value, TimeSpan.FromMinutes(1)); Assert.True(c.TryGet<object>("ok", out var found)); Assert.Same(value, found); c.Clear(); Assert.False(c.TryGet<object>("ok", out _));
    }
    [Fact] public void LocalFarmlandConfigurationOverridesIncomingApiEntryAcrossSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-priority-" + Guid.NewGuid());
        try
        {
            var service = new MvpCatalogService(new PersistenceService(root)); var local = service.Entries.First();
            local.Map = "farmland_map"; local.CustomMin = 180; local.CustomMax = 240; local.FarmlandConfirmed = true;
            service.Merge(new[] { new CatalogEntry { Key = "api:iro:test:farmland_map", MonsterId = local.MonsterId, Name = local.Name, Map = local.Map, MinMinutes = 30, MaxMinutes = 30 } });
            var merged = service.Entries.Single(e => e.Key.StartsWith("api:")); var result = new MvpRespawnService().Resolve(merged, true)!.Value;
            Assert.Equal(180, result.Min); Assert.Equal(240, result.Max); Assert.Contains("Farmland", result.Origin);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task RetryAfterAndResetUseTheLongerPause()
    {
        var h = new Handler { Responder = _ =>
        {
            var r = Response(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            r.Headers.Add("X-RateLimit-Remaining", "0"); r.Headers.Add("X-RateLimit-Reset", "1"); return Task.FromResult(r);
        } };
        using var c = Client(h); var now = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<RagnaPlaceException>(() => c.SearchAsync("iro", "Poring")); Assert.True(c.RetryAllowedUtc >= now.AddSeconds(89));
    }
    [Fact] public void SyncKeepsExplicitApiFavoriteWhenApplyingLocalFarmlandOverride()
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-favorite-" + Guid.NewGuid());
        try
        {
            var catalog = new MvpCatalogService(new PersistenceService(root)); var local = catalog.Entries.First();
            local.CustomMin = 120; local.CustomMax = 180; local.FarmlandConfirmed = true;
            string key = "api:iro:" + local.MonsterId;
            catalog.Entries.Add(new CatalogEntry { Key = key, MonsterId = local.MonsterId, Name = local.Name, Favorite = true });
            catalog.Merge(new[] { new CatalogEntry { Key = key, MonsterId = local.MonsterId, Name = local.Name, MinMinutes = 30, MaxMinutes = 30 } });
            var merged = catalog.Entries.Single(e => e.Key == key); Assert.True(merged.Favorite); Assert.Equal(120, merged.CustomMin);
        }
        finally { Directory.Delete(root, true); }
    }
}
