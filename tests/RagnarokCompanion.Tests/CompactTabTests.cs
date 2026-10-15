using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class CompactTabTests
{
    static MainViewModel Create(string? root=null)
    {
        var store=new PersistenceService(root??Path.Combine(Path.GetTempPath(),"Farmland-tabs-test-"+Guid.NewGuid()));
        store.Save("spotlight-catalog.json",SpotlightDiagnostics.Synthetic());
        return new MainViewModel(store);
    }
    static void Dispose(MainViewModel vm){vm.Catalog.Dispose();vm.Spotlight.Dispose();}
    [Fact] public void TabChangesKeepTimerServicesAndTimestampsAndPersistAcrossRestart()
    {
        var vm=Create();
        try
        {
            var engine=vm.Engine;var schedule=vm.Events.Schedule;var now=DateTimeOffset.UtcNow;
            engine.StartMining(now);engine.Mvps.Add(new(){Name="Teste",Map="test",DeathUtc=now,MinMinutes=60,MaxMinutes=90});
            foreach(var tab in Enum.GetValues<CompactTab>())
            {
                vm.SelectCompactTabCommand.Execute(tab.ToString());Assert.Equal(tab,vm.ActiveCompactTab);Assert.Same(engine,vm.Engine);Assert.Same(schedule,vm.Events.Schedule);Assert.Equal(now,engine.Mining.EnteredUtc);Assert.Equal(now,engine.Mvps[0].DeathUtc);
            }
            vm.ActiveCompactTab=CompactTab.Events;
            var restarted=Create(vm.Store.Root);
            try{Assert.Equal(CompactTab.Events,restarted.ActiveCompactTab);}finally{Dispose(restarted);}
            vm.SelectCompactTabCommand.Execute("invalid");Assert.Equal(CompactTab.Events,vm.ActiveCompactTab);
        }
        finally{Dispose(vm);}
    }
    [Fact] public void EveryMonitoredMvpRemainsScrollableDespiteLegacyDisplayMode()
    {
        var vm=Create();try
        {
            vm.Settings.CompactMode="Mineração";vm.Settings.CompactCount=3;
            for(int n=5;n>=1;n--)vm.Engine.Mvps.Add(new(){Name="Teste "+n,Map="test",DeathUtc=DateTimeOffset.UtcNow.AddDays(-1),MinMinutes=n*60,MaxMinutes=n*60});
            vm.Refresh();Assert.Equal(5,vm.CompactAllRows.Count);Assert.False(vm.NoMonitoredMvps);Assert.Equal(144,vm.CompactMvpViewport);Assert.Equal(new[]{"Teste 1","Teste 2","Teste 3","Teste 4","Teste 5"},vm.CompactAllRows.Select(r=>r.Name));
        }finally{Dispose(vm);}
    }
    [Fact] public void InactiveTabsStillNotifyAndNeverChangeSelectedTab()
    {
        var vm=Create();try
        {
            vm.ActiveCompactTab=CompactTab.Spotlight;
            var now=new DateTimeOffset(2026,10,15,19,55,0,TimeSpan.FromHours(-3));int eventAlerts=0,mineAlerts=0;
            vm.Events.Schedule.Alert+=_=>eventAlerts++;vm.Engine.Alert+=_=>mineAlerts++;
            vm.Engine.StartMining(now.AddMinutes(-31));vm.Engine.Evaluate(now);vm.Events.Schedule.Evaluate(now);vm.Refresh();
            Assert.Equal(1,eventAlerts);Assert.Equal(1,mineAlerts);Assert.Equal(CompactTab.Spotlight,vm.ActiveCompactTab);
        }finally{Dispose(vm);}
    }
    [Fact] public void AttentionUsesDatesWithoutChangingTimers()
    {
        var now=DateTimeOffset.UtcNow;var engine=new TimerService(new(),new(),new(),new());Assert.False(CompactAttentionService.Mining(engine,now));engine.StartMining(now);
        Assert.False(CompactAttentionService.Mining(engine,now.AddMinutes(29)));Assert.True(CompactAttentionService.Mining(engine,now.AddMinutes(30)));Assert.False(CompactAttentionService.Mining(engine,now.AddMinutes(36)));Assert.True(CompactAttentionService.Mining(engine,now.AddMinutes(90)));
        engine.Mvps.Add(new(){Name="Teste",DeathUtc=now,MinMinutes=60,MaxMinutes=90});Assert.False(CompactAttentionService.Mvps(engine,now.AddMinutes(54)));Assert.True(CompactAttentionService.Mvps(engine,now.AddMinutes(55)));
        var repo=new EventRepository(Path.Combine(Path.GetTempPath(),"Farmland-badge-test-"+Guid.NewGuid()));var schedule=new EventScheduleService(repo);var before=new DateTimeOffset(2026,10,15,19,54,0,TimeSpan.FromHours(-3));
        Assert.False(CompactAttentionService.Events(schedule,before));Assert.True(CompactAttentionService.Events(schedule,before.AddMinutes(1)));
    }
    [Fact] public void EventTabHasSeparateNextDuduAndKvmAndRespectsDisabledEvents()
    {
        var repo=new EventRepository(Path.Combine(Path.GetTempPath(),"Farmland-next-events-"+Guid.NewGuid()));var now=new DateTimeOffset(2026,10,15,19,58,0,TimeSpan.FromHours(-3));var vm=new EventViewModel(repo,()=>now);
        Assert.Equal("20:30",vm.NextDudu!.Time);Assert.Equal("20:00",vm.NextKvm!.Time);Assert.Contains("Abertura",vm.NextKvm.Timing);
        repo.Events.Single(e=>e.OfficialId=="dudu").Enabled=false;vm.Refresh();Assert.Null(vm.NextDudu);Assert.NotNull(vm.NextKvm);
    }
    [Fact] public void UnknownSavedTabFallsBackToMining()
    {
        var settings=new Settings{CompactTab=(CompactTab)200};PersistenceService.Validate(settings);Assert.Equal(CompactTab.Mining,settings.CompactTab);
    }
}
