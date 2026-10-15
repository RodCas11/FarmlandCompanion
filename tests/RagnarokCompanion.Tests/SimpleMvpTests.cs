using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class SimpleMvpTests
{
    static DateTimeOffset Now {get;}=new(new DateTime(2020,1,15,12,0,0),TimeZoneInfo.Local.GetUtcOffset(new DateTime(2020,1,15,12,0,0)));
    static MainViewModel Create(Func<string,bool>? confirm=null,string? root=null)=>new(new PersistenceService(root??Path.Combine(Path.GetTempPath(),"Farmland-simple-mvp-"+Guid.NewGuid())),()=>Now,confirm??(_=>false));
    static void Dispose(MainViewModel vm){vm.Catalog.Dispose();vm.Spotlight.Dispose();}
    static CatalogEntry Configure(MainViewModel vm,double min=60,double max=90)
    {
        var c=vm.Catalog;c.SelectedEntry=c.Entries.First();c.RespawnMin=min;c.RespawnMax=max;c.SaveRespawnCommand.Execute(null);return c.SelectedEntry!;
    }
    [Fact] public void FavoriteStarPersistsWithoutStartingTimerAndSearchIsInstant()
    {
        var vm=Create();try
        {
            var c=vm.Catalog;var entry=c.Entries.Last();c.ToggleFavoriteCommand.Execute(entry);Assert.True(entry.Favorite);Assert.Empty(vm.Engine.Mvps);Assert.Equal(entry.Key,c.Entries[0].Key);Assert.Equal("Sem horário",Assert.Single(c.CompactFavorites).Status);
            c.Query=entry.Name.ToLowerInvariant();Assert.Contains(c.Entries,e=>e.Key==entry.Key);c.Query="nothing matches";Assert.Empty(c.Entries);c.Query="";
            c.Query="osíris";Assert.Contains(c.Entries,e=>e.Name=="Osiris");c.Query="";
            var restart=Create(root:vm.Store.Root);try{Assert.Single(restart.Catalog.FavoriteEntries);Assert.Empty(restart.Engine.Mvps);}finally{Dispose(restart);}
            c.ToggleFavoriteCommand.Execute(entry);Assert.Empty(c.FavoriteEntries);Assert.Empty(c.CompactFavorites);
        }finally{Dispose(vm);}
    }
    [Fact] public void Register1032UsesTodayAndExistingRangeAndPersistsImmediately()
    {
        var vm=Create();try
        {
            var entry=Configure(vm);var c=vm.Catalog;c.ToggleFavoriteCommand.Execute(entry);c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);
            var timer=Assert.Single(vm.Engine.Mvps);Assert.Equal(new DateTime(2020,1,15,10,32,0),timer.DeathUtc!.Value.ToLocalTime().DateTime);Assert.Equal(timer.DeathUtc.Value.AddMinutes(60),timer.StartUtc);Assert.Equal(timer.DeathUtc.Value.AddMinutes(90),timer.EndUtc);Assert.Single(c.CompactFavorites);Assert.NotEqual("Sem horário",c.CompactFavorites[0].Status);
            var restart=Create(root:vm.Store.Root);try{Assert.Equal(timer.DeathUtc,Assert.Single(restart.Engine.Mvps).DeathUtc);}finally{Dispose(restart);}
        }finally{Dispose(vm);}
    }
    [Fact] public void YesterdayIsExplicitAndFutureTodayNeverBecomesYesterdayAutomatically()
    {
        var vm=Create();try
        {
            Configure(vm);var c=vm.Catalog;c.DeathTime="23:45";c.RegisterDeathCommand.Execute(null);Assert.Empty(vm.Engine.Mvps);Assert.Contains("futuro",c.Status);
            c.PreviousDay=true;c.RegisterDeathCommand.Execute(null);Assert.Equal(new DateTime(2020,1,14,23,45,0),Assert.Single(vm.Engine.Mvps).DeathUtc!.Value.ToLocalTime().DateTime);
        }finally{Dispose(vm);}
    }
    [Theory] [InlineData("25:00")] [InlineData("10:99")] [InlineData("1032")] [InlineData("")]
    public void InvalidShortTimeDoesNotCreateTimer(string text)
    {
        var vm=Create();try{Configure(vm);vm.Catalog.DeathTime=text;vm.Catalog.RegisterDeathCommand.Execute(null);Assert.Empty(vm.Engine.Mvps);}finally{Dispose(vm);}
    }
    [Fact] public void AutomaticRespawnNeedsNoSetupAndAdvancedOverrideCanOmitMaximum()
    {
        var vm=Create();try
        {
            var c=vm.Catalog;c.SelectedEntry=c.Entries.First();c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);Assert.False(c.NeedsRespawn);Assert.True(Assert.Single(vm.Engine.Mvps).HasRespawn);
            c.RespawnMin=120;c.RespawnMax=null;c.SaveRequiredRespawnCommand.Execute(null);Assert.False(c.NeedsRespawn);Assert.Equal(120,Assert.Single(vm.Engine.Mvps).MaxMinutes);
            c.DeathTime="11:00";c.RegisterDeathCommand.Execute(null);Assert.Single(vm.Engine.Mvps);Assert.Equal(11,vm.Engine.Mvps[0].DeathUtc!.Value.ToLocalTime().Hour);
        }finally{Dispose(vm);}
    }
    [Fact] public void OlderDeathRequiresConfirmationButNewerDeathReusesSameSpawnTimer()
    {
        bool answer=false;int prompts=0;var vm=Create(_=>{prompts++;return answer;});try
        {
            Configure(vm);var c=vm.Catalog;c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);var timer=Assert.Single(vm.Engine.Mvps);var id=timer.Id;
            c.DeathTime="09:32";c.RegisterDeathCommand.Execute(null);Assert.Equal(1,prompts);Assert.Equal(10,timer.DeathUtc!.Value.ToLocalTime().Hour);
            answer=true;c.RegisterDeathCommand.Execute(null);Assert.Equal(9,timer.DeathUtc!.Value.ToLocalTime().Hour);
            c.DeathTime="11:32";c.RegisterDeathCommand.Execute(null);Assert.Equal(2,prompts);Assert.Equal(id,Assert.Single(vm.Engine.Mvps).Id);
        }finally{Dispose(vm);}
    }
    [Fact] public void CompactShowsAtMostFiveFavoritesWithActiveTimersFirstAndManualChoice()
    {
        var vm=Create();try
        {
            var c=vm.Catalog;var entries=c.Entries.Take(7).ToArray();foreach(var e in entries)c.ToggleFavoriteCommand.Execute(e);Assert.Equal(5,c.CompactFavorites.Count);
            vm.Engine.Mvps.Add(new(){CatalogKey=entries[6].Key,Name=entries[6].Name,Map="test",DeathUtc=DateTimeOffset.UtcNow,MinMinutes=15,MaxMinutes=15});vm.Engine.Mvps.Add(new(){CatalogKey=entries[5].Key,Name=entries[5].Name,Map="test",DeathUtc=DateTimeOffset.UtcNow,MinMinutes=5,MaxMinutes=10});vm.Refresh();
            Assert.Equal(entries[5].Key,c.CompactFavorites[0].Entry.Key);Assert.Equal(entries[6].Key,c.CompactFavorites[1].Entry.Key);Assert.Contains(c.CompactFavorites,r=>r.Status=="Sem horário");
            c.CustomCompactSelection=true;var excluded=entries.First(e=>!c.IsCompactChosen(e.Key));c.ChooseCompact(excluded.Key,true);Assert.Equal(5,vm.Settings.CompactFavoriteKeys.Count);
            c.ChooseCompact(c.CompactFavorites[0].Entry.Key,false);c.ChooseCompact(excluded.Key,true);Assert.Contains(c.CompactFavorites,r=>r.Entry.Key==excluded.Key);vm.Save();
            var restart=Create(root:vm.Store.Root);try{Assert.True(restart.Catalog.CustomCompactSelection);Assert.Equal(5,restart.Catalog.CompactFavorites.Count);Assert.Contains(restart.Catalog.CompactFavorites,r=>r.Entry.Key==excluded.Key);}finally{Dispose(restart);}
        }finally{Dispose(vm);}
    }
    [Fact] public void ClearCanCancelAndKeepsFavoritesRespawnsHistoryAndTimersConfiguration()
    {
        bool answer=false;var vm=Create(_=>answer);try
        {
            var entry=Configure(vm);var c=vm.Catalog;c.ToggleFavoriteCommand.Execute(entry);c.DeathTime="10:32";c.RegisterDeathCommand.Execute(null);var history=vm.Engine.History.Select(h=>h.Message).ToArray();
            c.ClearTimersCommand.Execute(null);Assert.NotNull(vm.Engine.Mvps[0].DeathUtc);answer=true;c.ClearTimersCommand.Execute(null);
            Assert.Null(vm.Engine.Mvps[0].DeathUtc);Assert.Equal(60,vm.Engine.Mvps[0].MinMinutes);Assert.Equal(90,entry.CustomMax);Assert.Single(c.FavoriteEntries);Assert.Equal(history,vm.Engine.History.Select(h=>h.Message));Assert.Equal("Sem horário",c.CompactFavorites[0].Status);
            var restart=Create(root:vm.Store.Root);try{Assert.Null(restart.Engine.Mvps[0].DeathUtc);Assert.Single(restart.Catalog.FavoriteEntries);Assert.Equal(history,restart.Engine.History.Select(h=>h.Message));}finally{Dispose(restart);}
        }finally{Dispose(vm);}
    }
    [Fact] public void ExistingManualTimersBecomeSearchableWithoutChangingDeathOrId()
    {
        var vm=Create();try
        {
            var timer=new Mvp{Name="MVP personalizado",Map="ponto_1",DeathUtc=Now.AddHours(-2),MinMinutes=60,MaxMinutes=90};vm.Engine.Mvps.Add(timer);vm.Refresh();var original=timer.DeathUtc;
            var c=vm.Catalog;c.Query="personalizado";var entry=Assert.Single(c.Entries);c.ToggleFavoriteCommand.Execute(entry);Assert.Equal(original,timer.DeathUtc);Assert.Equal("manual:"+timer.Id,timer.CatalogKey);
            c.SelectedEntry=entry;c.DeathTime="11:00";c.RegisterDeathCommand.Execute(null);Assert.Same(timer,Assert.Single(vm.Engine.Mvps));Assert.Single(c.CompactFavorites);vm.Save();
            var restart=Create(root:vm.Store.Root);try{Assert.Single(restart.Catalog.FavoriteEntries);Assert.Equal(timer.Id,Assert.Single(restart.Engine.Mvps).Id);}finally{Dispose(restart);}
        }finally{Dispose(vm);}
    }
}
