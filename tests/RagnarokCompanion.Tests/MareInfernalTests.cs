using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;

public sealed class MareInfernalTests
{
    static EventRepository Repo(){ var repo=new EventRepository(Path.Combine(Path.GetTempPath(),"Farmland-mare-"+Guid.NewGuid())); /* Explicit custom coincidences exercise grouping independently of official schedules. */ var dudu=repo.Events.Single(e=>e.OfficialId=="dudu"); dudu.Times=new(){"00:30","05:30","10:30","15:30","20:30","22:30"}; dudu.TimesCustomized=true; return repo; }
    static DateTimeOffset At(int hour,int minute,int day=15,int second=0)=>new(2026,10,day,hour,minute,second,TimeSpan.FromHours(-3));
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(4)] [InlineData(6)] [InlineData(8)] [InlineData(10)]
    [InlineData(12)] [InlineData(14)] [InlineData(16)] [InlineData(18)] [InlineData(20)] [InlineData(22)]
    public void EveryOccurrenceNotifiesFiveMinutesBeforeWithoutDuplicates(int hour)
    {
        var repo=Repo();var service=new EventScheduleService(repo);var batches=new List<IReadOnlyList<EventAlert>>();service.AlertBatch+=batches.Add;
        var start=At(hour,30);service.Evaluate(start.AddMinutes(-5).AddSeconds(-1));Assert.Empty(batches);
        service.Evaluate(start.AddMinutes(-5));var batch=Assert.Single(batches);Assert.Single(batch,a=>a.EventId=="official-mare-infernal");
        var popup=EventNotificationComposer.Compose(batch,repo,start.AddMinutes(-5))!;Assert.Contains("Maré Infernal",popup.Message);Assert.Contains("em 5 minutos!",popup.Message);Assert.Contains($"{hour:00}:30",popup.Message);
        Assert.Equal(new[]{0,10,20,22}.Contains(hour)?2:1,batch.Count);Assert.Equal(batch.Count,repo.History.Count);
        service.Evaluate(start.AddMinutes(-5));Assert.Single(batches);
    }
    [Fact] public void TwelveDailyTimesAndMidnightRescheduleKeepSimultaneousEventsSeparate()
    {
        var repo=Repo();var service=new EventScheduleService(repo);var today=service.Occurrences(At(22,30),true);
        Assert.Equal(20,today.Count);Assert.Equal(Enumerable.Range(0,12).Select(n=>$"{n*2:00}:30"),today.Where(e=>e.Event.OfficialId=="mare-infernal").Select(e=>e.Time));
        var next=service.Upcoming(At(22,30));Assert.Equal(At(0,30,16),next[0].StartUtc);Assert.Equal(next[0].StartUtc,next[1].StartUtc);Assert.Equal(2,next.Take(2).Select(o=>o.Event.Id).Distinct().Count());
    }
    [Fact] public void GroupHasOnePopupAndIndividualHistoryAcrossRestart()
    {
        var repo=Repo();var engine=new EventScheduleService(repo);var batches=new List<IReadOnlyList<EventAlert>>();engine.AlertBatch+=batches.Add;
        engine.Evaluate(At(10,25));var group=Assert.Single(batches);Assert.Equal(2,group.Count);Assert.Equal(2,repo.History.Count);
        Assert.Contains(repo.History,h=>h.EventName=="Fantasminhas Dudu" && h.AdvanceMinutes==5);Assert.Contains(repo.History,h=>h.EventName=="Maré Infernal" && h.AdvanceMinutes==5);
        var popup=EventNotificationComposer.Compose(group,repo,At(10,25))!;Assert.Contains("Fantasminhas Dudu",popup.Message);Assert.Contains("Maré Infernal",popup.Message);Assert.Contains("começam em 5 minutos!",popup.Message);
        var restart=new EventScheduleService(new EventRepository(repo.Store.Root));restart.AlertBatch+=batches.Add;restart.Evaluate(At(10,26));Assert.Single(batches);
        restart.Evaluate(At(10,25,16));Assert.Equal(2,batches.Count);
    }
    [Fact] public void PreferencesAreAppliedPerMemberAtSchedulingAndDelivery()
    {
        var repo=Repo();var mare=repo.Events.Single(e=>e.OfficialId=="mare-infernal");var dudu=repo.Events.Single(e=>e.OfficialId=="dudu");
        var service=new EventScheduleService(repo);IReadOnlyList<EventAlert>? batch=null;service.AlertBatch+=a=>batch=a;service.Evaluate(At(10,25));
        mare.AlertsEnabled=false;var popup=EventNotificationComposer.Compose(batch!,repo,At(10,26))!;Assert.DoesNotContain("Maré Infernal",popup.Message);Assert.Contains("Dudu",popup.Message);Assert.Contains("04:00",popup.Message);
        dudu.SoundsEnabled=false;Assert.False(EventNotificationComposer.Compose(batch!,repo,At(10,26))!.Sounds);
        mare.AlertsEnabled=true;mare.SoundsEnabled=true;Assert.True(EventNotificationComposer.Compose(batch!,repo,At(10,26))!.Sounds);
        repo.Settings.Sounds=false;Assert.False(EventNotificationComposer.Compose(batch!,repo,At(10,26))!.Sounds);
        dudu.Enabled=false;mare.Enabled=false;Assert.Null(EventNotificationComposer.Compose(batch!,repo,At(10,26)));
        Assert.Null(EventNotificationComposer.Compose(batch!,repo,At(10,30)));
        repo=Repo();repo.Events.Single(e=>e.OfficialId=="mare-infernal").AlertsEnabled=false;
        service=new EventScheduleService(repo);service.AlertBatch+=a=>batch=a;service.Evaluate(At(10,25));Assert.Single(batch!);Assert.Equal("official-dudu",batch![0].EventId);
    }
    [Fact] public void CustomLeadProducesSeparateWindowsAndPreservesExactSoloMessage()
    {
        var repo=Repo();var mare=repo.Events.Single(e=>e.OfficialId=="mare-infernal");mare.UseGlobalAdvances=false;mare.AdvanceMinutes=new(){10};
        var service=new EventScheduleService(repo);var batches=new List<IReadOnlyList<EventAlert>>();service.AlertBatch+=batches.Add;
        service.Evaluate(At(10,20));Assert.Single(batches);Assert.Equal("official-mare-infernal",Assert.Single(batches[0]).EventId);
        service.Evaluate(At(10,25));Assert.Equal(2,batches.Count);Assert.Equal("official-dudu",Assert.Single(batches[1]).EventId);
        service.Evaluate(At(2,20,16));var last=batches.Last();var popup=EventNotificationComposer.Compose(last,repo,At(2,25,16))!;
        Assert.StartsWith("Maré Infernal começa em 5 minutos! Prepare-se para o evento.",popup.Message);
    }
    [Fact] public void UpgradeAddsMareOnceWithoutChangingExistingPreferencesOrHistory()
    {
        var repo=Repo();repo.Events.RemoveAll(e=>e.OfficialId=="mare-infernal");var dudu=repo.Events.Single(e=>e.OfficialId=="dudu");dudu.Times=new(){"12:34"};dudu.TimesCustomized=true;dudu.AlertsEnabled=false;
        repo.History.Add(new(){Key="preserved",OccurrenceUtc=At(12,34)});repo.Settings.Sounds=false;repo.Save();
        var upgrade=new EventRepository(repo.Store.Root);Assert.Equal(3,upgrade.Events.Count);Assert.Single(upgrade.Events,e=>e.OfficialId=="mare-infernal");Assert.Equal("12:34",Assert.Single(upgrade.Events.Single(e=>e.OfficialId=="dudu").Times));Assert.False(upgrade.Settings.Sounds);Assert.Contains(upgrade.History,h=>h.Key=="preserved");
        var vm=new EventViewModel(upgrade,()=>At(19,58));vm.SelectedEvent=upgrade.Events.Single(e=>e.OfficialId=="mare-infernal");vm.EditSounds=false;vm.SaveEventCommand.Execute(null);vm.MareAlerts=false;
        var restart=new EventRepository(repo.Store.Root);var saved=restart.Events.Single(e=>e.OfficialId=="mare-infernal");Assert.False(saved.SoundsEnabled);Assert.False(saved.AlertsEnabled);Assert.Equal(12,saved.Times.Count);
    }
    [Fact] public void CompactUpcomingIsChronologicalIncludesAllTypesAndKeepsRowIdentity()
    {
        var repo=Repo();var now=At(19,58);var vm=new EventViewModel(repo,()=>now);
        var first=vm.CompactUpcoming.Take(3).Select(r=>r.Occurrence).ToArray();Assert.Equal(new[]{"kvm","dudu","mare-infernal"},first.Select(o=>o.Event.OfficialId));Assert.Equal(first[1].StartUtc,first[2].StartUtc);
        var row=vm.CompactUpcoming[0];now=now.AddSeconds(1);vm.Refresh();Assert.Same(row,vm.CompactUpcoming[0]);Assert.Equal("00:01:59",row.Occurrence.Countdown);
        repo.Events.Add(new(){Name="Personalizado",Times=new(){"19:59"}});vm.Refresh();Assert.Equal("Personalizado",vm.CompactUpcoming[0].Occurrence.Event.Name);
        Assert.True(vm.Today.Select(r=>r.StartUtc).SequenceEqual(vm.Today.Select(r=>r.StartUtc).OrderBy(t=>t)));
    }
}
