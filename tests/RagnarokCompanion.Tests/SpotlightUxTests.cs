using System.ComponentModel;
using System.Threading;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class SpotlightUxTests
{
    static PersistenceService Store()=>new(Path.Combine(Path.GetTempPath(),"Farmland-spotlight-ux-"+Guid.NewGuid()));
    static void Sta(Action run){Exception? error=null;var thread=new Thread(()=>{try{run();}catch(Exception ex){error=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;}
    [Fact] public void RootJsonEmbedsAllSevenGroupsAndExactPublishedCounts(){
        var catalog=SpotlightExtractedCatalog.LoadBundled();Assert.Equal(new[]{39,37,39,39,38,38,38},catalog.Groups.Select(g=>g.Maps.Count));Assert.Equal(268,catalog.Groups.Sum(g=>g.Maps.Count));Assert.Equal(37,catalog.Groups.Single(g=>g.Name=="Spotlight Lunar").Maps.Count);
        Assert.All(catalog.Groups.SelectMany(g=>g.Maps),map=>{Assert.Null(map.MapUrl);Assert.Empty(map.Monsters);});
        using var service=new SpotlightCatalogService(Store());Assert.Equal(7,service.LoadLocal()!.Groups.Count);
    }
    [Fact] public void MissingFieldsStayMissingAndRecommendationIsNeverRecalculated(){
        const string json="""{"fonte":"https://farmland.net.br/wiki/index.php/SpotLight","extraido_em":"2026-10-15T19:26:18Z","grupos":[{"nome":"Spotlight Exemplo","mapas":[{"codigo":"some_map","nome":"Mapa","nivel_maximo":100,"nivel_recomendado":72},{"codigo":"empty_map"}]}]}""";
        var catalog=SpotlightExtractedCatalog.Parse(json);Assert.Equal(72,catalog.Groups[0].Maps[0].Recommended);var absent=catalog.Groups[0].Maps[1];Assert.Null(absent.MinLevel);Assert.Null(absent.MaxLevel);Assert.Null(absent.Recommended);Assert.False(absent.HasMapLink);Assert.Contains("não informados",absent.MonsterInfo);
    }
    [Fact] public void SavedVerifiedDetailsSupplementWithoutChangingJsonLevels(){
        var primary=SpotlightExtractedCatalog.LoadBundled();var target=primary.Groups[0].Maps[0];int? expected=target.Recommended;
        var saved=SpotlightDiagnostics.Synthetic();saved.Groups[0].Maps[0].Code=target.Code;saved.Groups[0].Maps[0].MapUrl="https://www.divine-pride.net/database/map/"+target.Code;
        SpotlightExtractedCatalog.Supplement(primary,saved);Assert.Equal(expected,target.Recommended);Assert.Single(target.Monsters);Assert.True(target.HasMapLink);Assert.Equal(7,primary.Groups.Count);
    }
    [Fact] public void BrowseAndConfirmationAreIndependentPersistOfflineAndExpire(){Sta(()=>{
        var store=Store();using var source=new SpotlightCatalogService(store);var time=new DateTimeOffset(2026,10,15,22,59,59,TimeSpan.Zero);using var vm=new SpotlightViewModel(store,source,()=>time);
        var first=vm.Groups[0];var other=vm.Groups[1];vm.SelectedGroup=first;Assert.True(vm.Pending);vm.Confirm();Assert.False(vm.Pending);vm.SelectedGroup=other;Assert.Equal(37,vm.Rows.Count);Assert.Equal(first.Id,vm.ConfirmedGroup!.Id);Assert.Contains(first.Name,vm.Summary);
        using var reload=new SpotlightViewModel(store,new SpotlightCatalogService(store),()=>time);Assert.Equal(other.Id,reload.SelectedGroup!.Id);Assert.Equal(first.Id,reload.ConfirmedGroup!.Id);time=time.AddSeconds(1);reload.Tick();Assert.True(reload.Pending);Assert.Equal(37,reload.Rows.Count);Assert.Equal(7,reload.Groups.Count);reload.Confirm();Assert.Equal(other.Id,reload.ConfirmedGroup!.Id);
    });}
    [Fact] public void SearchCombinedFiltersSortAndClearUsePublishedLevels(){Sta(()=>{
        var store=Store();using var vm=new SpotlightViewModel(store,new SpotlightCatalogService(store));
        foreach(var group in vm.Groups){vm.SelectedGroup=group;Assert.Equal(group.Maps.Count,vm.Rows.Count);}
        var lunar=vm.Groups.Single(g=>g.Name=="Spotlight Lunar");vm.SelectedGroup=lunar;var sample=lunar.Maps.First(m=>m.Recommended>=50&&m.Recommended<=79);vm.Search=sample.Code;Assert.Single(vm.Rows);vm.Search=sample.Name.ToUpperInvariant();Assert.Contains(vm.Rows,r=>r.Map.Code==sample.Code);vm.Filter=vm.Filters[2];vm.CharacterLevel=sample.MinLevel;Assert.True(vm.Rows.Single().Suitable);vm.Filter=vm.Filters[5];Assert.Empty(vm.Rows);vm.ClearFiltersCommand.Execute(null);Assert.Equal(37,vm.Rows.Count);Assert.Null(vm.CharacterLevel);
        vm.Maps.SortDescriptions.Add(new("Map.Recommended",ListSortDirection.Ascending));var ordered=vm.Maps.Cast<SpotlightRow>().Select(r=>r.Map.Recommended);Assert.Equal(ordered.OrderBy(x=>x),ordered);
    });}
    [Fact] public void MapLinksRemainIndividualAndMissingLinksStayDisabled(){
        var catalog=SpotlightDiagnostics.Synthetic();var one=catalog.Groups[0].Maps[0];var two=catalog.Groups[0].Maps[1];one.MapUrl="https://www.divine-pride.net/database/map/test_map";two.MapUrl="https://www.divine-pride.net/database/map/test_map2";Assert.NotEqual(one.DivinePrideUrl,two.DivinePrideUrl);Assert.True(one.HasMapLink);one.MapUrl="javascript:alert(1)";Assert.False(one.HasMapLink);one.MapUrl=null;Assert.False(one.HasMapLink);
    }
}
