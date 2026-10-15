using System.Net;
using System.Net.Http;
using System.Threading;
using RagnarokCompanion.Services;
using RagnarokCompanion.Models;
using Xunit;
namespace RagnarokCompanion.Tests;
public class SpotlightTests
{
 const string Html="""
 <option value="Spotlight Teste">Teste</option>
 "wgRevisionId":123,
 const monsters = {"test_map":[["Teste",90,"MVP"]]};
 const test = [["test_map","Mapa de teste",80,100],["test_low","Inicial",1,10]];
 const lists = {"Spotlight Teste": test};
 function recommended(max){return Math.max(1,max-15)}
 """;
 [Fact] public void ParserPreservesPublishedLevelsAndMonsters()
 {
  var c=SpotlightCatalogService.Parse(Html,DateTimeOffset.UtcNow);
  Assert.Equal(123,c.Revision);Assert.Equal(2,c.Groups.Single().Maps.Count);
  var m=c.Groups[0].Maps[0];Assert.Equal(80,m.MinLevel);Assert.Equal(100,m.MaxLevel);Assert.Equal(85,m.Recommended);Assert.Contains("MVP",m.MonsterInfo);
  Assert.Equal(1,c.Groups[0].Maps[1].Recommended);Assert.EndsWith("/test_map",m.DivinePrideUrl);
 }
 [Theory]
 [InlineData("const test =", "const missing =")]
 [InlineData("Math.max(1,max-15)", "max-10")]
 [InlineData("80,100", "110,100")]
 public void ChangedOrInvalidSourceIsRejected(string before,string after)=>Assert.ThrowsAny<Exception>(()=>SpotlightCatalogService.Parse(Html.Replace(before,after),DateTimeOffset.UtcNow));
 [Fact] public void RotationUsesSaoPauloAndExactBoundary()
 {
  var before=new DateTimeOffset(2026,10,15,22,59,59,TimeSpan.Zero);
  Assert.Equal("00:00:01",SpotlightRotationService.Countdown(before));Assert.False(SpotlightRotationService.IsPending(before,before));
  Assert.True(SpotlightRotationService.IsPending(before,before.AddSeconds(1)));Assert.Equal("24:00:00",SpotlightRotationService.Countdown(before.AddSeconds(1)));
  Assert.True(SpotlightRotationService.IsPending(before,before.AddDays(3)));Assert.True(SpotlightRotationService.IsPending(null,before));
  Assert.Equal(SpotlightRotationService.Next(before),SpotlightRotationService.Next(before.ToOffset(TimeSpan.FromHours(9))));
 }
 [Fact] public void SelectionSearchSortingOfflineAndExpiry()
 {
  Exception? failure=null;var thread=new Thread(()=>{try{SpotlightDiagnostics.Verify(SpotlightDiagnostics.Synthetic());}catch(Exception e){failure=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();Assert.Null(failure);
 }
 [Fact] public async Task InvalidRefreshPreservesOfflineCacheAndThrottles()
 {
  var store=new PersistenceService(Path.Combine(Path.GetTempPath(),"spotlight-test-"+Guid.NewGuid()));var original=SpotlightDiagnostics.Synthetic();store.Save("spotlight-catalog.json",original);
  var handler=new FakeHandler();using var client=new HttpClient(handler);using var service=new SpotlightCatalogService(store,client);
  await Assert.ThrowsAnyAsync<Exception>(()=>service.RefreshAsync());Assert.Equal(2,service.LoadLocal()!.Groups.Count);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RefreshAsync());Assert.Equal(1,handler.Calls);
 }
 [Fact] public void NullCacheIdentifiersAreRejected()
 {
  var c=SpotlightDiagnostics.Synthetic();c.Groups[0].Id=null!;Assert.False(SpotlightCatalogService.Valid(c));
 }
 sealed class FakeHandler:HttpMessageHandler
 {
  public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("invalid page")});}
 }
}
