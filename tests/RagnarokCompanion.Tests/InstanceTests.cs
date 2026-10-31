using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class InstanceTests
{
    static PersistenceService Store()=>new(Path.Combine(Path.GetTempPath(),"Farmland-instances-"+Guid.NewGuid()));
    static void Dispose(MainViewModel vm){vm.Items.Dispose();vm.Catalog.Dispose();vm.Spotlight.Dispose();vm.Updates.Dispose();}
    [Fact] public void OfficialInstanceTimersPersistCatchUpAndNeverNotifyTwice()
    {
        var store=Store();var service=new InstanceTimerService(store);var now=new DateTimeOffset(2026,10,31,23,32,0,TimeSpan.FromHours(-3));
        Assert.Equal(7,InstanceCatalog.Entries.Length);Assert.Equal(612,InstanceCatalog.Entries.Single(d=>d.Id=="poring-village").NpcSpriteId);var captain=InstanceCatalog.Entries.Single(d=>d.Id=="tower-captain");Assert.Equal(709,captain.NpcSpriteId);Assert.Equal(128,captain.NpcReferenceId);
        foreach(var d in InstanceCatalog.Entries){Assert.Equal("Disponível",service.Status(d,now));service.Finish(d.Id,now,now);Assert.Equal("Em espera",service.Status(d,now));Assert.Equal(now.AddHours(d.CooldownHours),service.AvailableAt(d));}
        var fired=new List<InstanceReadyAlert>();service.Ready+=fired.Add;service.Evaluate(now.AddHours(4));Assert.Equal("Vila Poring",Assert.Single(fired).Name);
        var restarted=new InstanceTimerService(store);restarted.Ready+=fired.Add;restarted.Evaluate(now.AddHours(8));restarted.Evaluate(now.AddHours(9));Assert.Equal(7,fired.Count);
        Assert.All(InstanceCatalog.Entries,d=>Assert.Equal("Liberada",restarted.Status(d,now.AddHours(9))));
        var again=new InstanceTimerService(store);again.Ready+=fired.Add;again.Evaluate(now.AddDays(1));Assert.Equal(7,fired.Count);
        store.Save("mvps.json",new[]{new Mvp{Name="Preservado",DeathUtc=now}});again.Clear("poring-village");Assert.Null(again.State("poring-village").FinishedUtc);Assert.NotNull(again.State("ghost-palace").FinishedUtc);again.Clear();Assert.All(again.Timers,t=>Assert.Null(t.FinishedUtc));Assert.NotNull(Assert.Single(store.Load<List<Mvp>>("mvps.json")).DeathUtc);
    }
    [Fact] public void InstanceManualFinishUsesOfficialCooldownAndRespectsSoundPreference()
    {
        var now=new DateTimeOffset(new DateTime(2026,10,30,12,0,0),TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026,10,30,12,0,0)));var store=Store();var vm=new MainViewModel(store,()=>now,_=>true);
        try{
            var row=vm.Instances.Rows.Single(r=>r.Definition.Id=="poring-village");row.TimeCommand.Execute(null);row.ManualTime="10:32";row.SaveTimeCommand.Execute(null);
            Assert.Equal("Em espera",row.Status);Assert.Equal("14:32",vm.Instances.Service.AvailableAt(row.Definition)!.Value.ToLocalTime().ToString("HH:mm"));
            var saved=vm.Instances.Service.State(row.Definition.Id).FinishedUtc;row.ManualTime="23:59";row.SaveTimeCommand.Execute(null);Assert.Equal(saved,vm.Instances.Service.State(row.Definition.Id).FinishedUtc);
            vm.Instances.Sounds=false;int notifications=0;vm.Instances.Alert+=(_,sound)=>{Assert.False(sound);notifications++;};now=now.AddHours(4);vm.Instances.Tick();vm.Instances.Tick();Assert.Equal(1,notifications);Assert.Equal("Liberada",row.Status);
            var restart=new MainViewModel(store,()=>now,_=>true);try{restart.Instances.Alert+=(_,_)=>notifications++;restart.Instances.Tick();Assert.Equal(1,notifications);Assert.Equal(saved,restart.Instances.Service.State(row.Definition.Id).FinishedUtc);}finally{Dispose(restart);}
            row.NowCommand.Execute(null);Assert.Equal(now,vm.Instances.Service.State(row.Definition.Id).FinishedUtc);row.ClearCommand.Execute(null);Assert.Equal("Disponível",row.Status);
        }finally{Dispose(vm);}
    }
    [Fact] public void LordOfTheDeadHasOfficialWindowAndWorksWithFavoritesAndCards()
    {
        var now=new DateTimeOffset(2026,10,30,13,0,0,TimeSpan.Zero);var vm=new MainViewModel(Store(),()=>now,_=>true);
        try{
            vm.Catalog.Query="Lord of the Dead";var card=Assert.Single(vm.Catalog.Cards);Assert.Equal(1373,card.Entry.MonsterId);Assert.Equal("Senhor dos Mortos",card.Name);Assert.Equal("niflheim",card.Map);
            card.FavoriteCommand.Execute(null);card.NowCommand.Execute(null);var timer=Assert.Single(vm.Engine.Mvps);Assert.Equal(now.AddHours(1),timer.StartUtc);Assert.Equal(now.AddMinutes(70),timer.EndUtc);Assert.Contains(vm.Catalog.CompactFavorites,r=>r.Entry.MonsterId==1373);
            vm.Catalog.Query="1373";Assert.Single(vm.Catalog.Cards);vm.Catalog.Query="Senhor dos Mortos";Assert.Single(vm.Catalog.Cards);
        }finally{Dispose(vm);}
    }
}
