using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class BuildSyncTests
{
 sealed class Server:HttpMessageHandler
 {
  public int CatalogReads,ContentReads,ProgressReads;public long Version=1;public bool Fails;
  object Doc(bool full){var f=new Dictionary<string,object>{{"buildId",new{stringValue=new string('a',32)}},{"title",new{stringValue="Build de teste"}},{"classId",new{stringValue="lord"}},{"stage",new{stringValue="Early"}},{"purpose",new{stringValue="PvM"}},{"authorName",new{stringValue="Autor de teste"}},{"ownerUid",new{stringValue="owner"}},{"version",new{integerValue=Version.ToString()}},{"updatedAt",new{stringValue="2026-11-07T12:00:00Z"}}};if(full)f["payload"]=new{stringValue=JsonSerializer.Serialize(BuildCatalog.New())};return new{name="projects/farmland-companion/databases/(default)/documents/approvedBuilds/test",fields=f,updateTime="2026-11-07T12:00:00Z"};}
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){await Task.Delay(10,ct);string body=request.Content==null?"":await request.Content.ReadAsStringAsync(ct);object result;
   if(request.RequestUri!.AbsolutePath.EndsWith(":runQuery")){CatalogReads++;Assert.Contains("\"select\"",body);Assert.DoesNotContain("\"fieldPath\":\"payload\"",body);Assert.Contains("\"limit\":20",body);if(Fails)return new(HttpStatusCode.TooManyRequests);result=new[]{new{document=Doc(false)}};}
   else if(request.RequestUri.AbsolutePath.EndsWith("/communityProgress/global")){ProgressReads++;result=new{updateTime="2026-11-07T12:00:00Z",fields=new{approvedKeys=new{arrayValue=new{values=new[]{new{stringValue="lord:Early"}}}},rewardUnlocked=new{booleanValue=false}}};}
   else{ContentReads++;result=Doc(true);}return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(result),Encoding.UTF8,"application/json")};
  }
 }
 static string Root(){string root=Path.Combine(Path.GetTempPath(),"FarmlandSync-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
 static FirebaseSession Session(string root)=>new(new(){ApiKey=new string('x',30),ProjectId="farmland-companion",AuthDomain="farmland-companion.firebaseapp.com"},root);
 [Fact] public async Task SummaryTtlVersionedContentAndOfflineProgressAvoidRepeatedReads()
 {
  string root=Root();var cache=new BuildRepository(root);var server=new Server();using var gateway=new FirestoreBuildGateway(Session(root),server);var sync=new BuildCommunitySyncService(cache,gateway,()=>true);
  var pages=await Task.WhenAll(sync.Catalog("lord","Early","PvM",false,false,null,default),sync.Catalog("lord","Early","PvM",false,false,null,default));Assert.Equal(1,server.CatalogReads);var summary=pages[0].Value.Builds.Single();Assert.False(summary.HasContent);await sync.Content(summary,default);await sync.Content(summary,default);Assert.Equal(1,server.ContentReads);
  var state=cache.SyncState(BuildCommunitySyncService.CatalogKey("lord","Early","PvM",false))!;cache.SaveSyncState(BuildCommunitySyncService.CatalogKey("lord","Early","PvM",false),state with{Updated=DateTimeOffset.UtcNow.AddHours(-13)},"test");server.Version=2;var fresh=await sync.Catalog("lord","Early","PvM",false,false,null,default);Assert.False(fresh.Value.Builds.Single().HasContent);await sync.Content(fresh.Value.Builds.Single(),default);Assert.Equal(2,server.ContentReads);
  await sync.Progress(false,default);await sync.Progress(false,default);Assert.Equal(1,server.ProgressReads);var offline=new BuildCommunitySyncService(cache,gateway,()=>false);Assert.Single((await offline.Progress(false,default)).Value.ApprovedCombinations);Assert.True((await offline.Content(fresh.Value.Builds.Single(),default)).HasContent);Assert.Equal(2,server.CatalogReads);Assert.Equal(2,server.ContentReads);
 }
 [Fact] public void DeletedPublicationsAreRemovedFromCacheWithoutDeletingDrafts(){var cache=new BuildRepository(Root());var draft=BuildCatalog.New();cache.Save(draft);cache.Cache(new[]{new CommunityBuild("removed",draft,"approved")});Assert.NotNull(cache.CommunityById("removed"));cache.ReconcileCommunity(draft.ClassId,draft.Stage,draft.Purpose,Array.Empty<CommunityBuild>());Assert.Null(cache.CommunityById("removed"));Assert.Empty(cache.SearchCommunity(draft.ClassId,draft.Stage,draft.Purpose,""));Assert.Single(cache.Load());}
 [Fact] public void CommunityDefaultsToAllFiltersAndCacheIncludesDifferentClasses(){string root=Root();var cache=new BuildRepository(root);var a=BuildCatalog.New();var b=BuildCatalog.New();b.ClassId="paladin";b.Stage="Mid";b.Purpose="MVP";cache.Cache(new[]{new CommunityBuild("a",a,"approved"),new CommunityBuild("b",b,"approved")});Assert.Equal(2,cache.SearchCommunity("","Todas","Todas","").Count);Assert.Single(cache.SearchCommunity("lord","Todas","Todas",""));using var vm=new RagnarokCompanion.ViewModels.BuildViewModel(new(root));Assert.Equal("",vm.CommunityClass.Id);Assert.Equal("Todas",vm.CommunityStage);Assert.Equal("Todas",vm.CommunityPurpose);}
 [Fact] public async Task RateLimitPersistsBackoffWithoutDroppingLocalData()
 {
  string root=Root();var cache=new BuildRepository(root);var server=new Server{Fails=true};using var gateway=new FirestoreBuildGateway(Session(root),server);var sync=new BuildCommunitySyncService(cache,gateway,()=>true);await sync.Catalog("lord","Early","PvM",false,true,null,default);await sync.Catalog("lord","Early","PvM",false,true,null,default);Assert.Equal(1,server.CatalogReads);var state=cache.SyncState(BuildCommunitySyncService.CatalogKey("lord","Early","PvM",false))!;Assert.Equal(1,state.Failures);Assert.True(state.RetryAfter>DateTimeOffset.UtcNow);
 }
}
