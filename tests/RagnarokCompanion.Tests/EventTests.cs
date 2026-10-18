using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;

public sealed class EventTests
{
    [Fact] public void UpdatedDuduTimesMigratePersistAndNotifyIncludingPreviousDay()
    {
        var root=Path.Combine(Path.GetTempPath(),"Farmland-dudu-update-"+Guid.NewGuid());
        var repo=new EventRepository(root);var dudu=repo.Events.Single(e=>e.OfficialId=="dudu");
        dudu.OfficialRevision=1;dudu.Times=new(){"00:30","05:30","10:30","15:30","20:30","22:30"};dudu.SoundsEnabled=false;repo.Save();
        repo=new EventRepository(root);dudu=repo.Events.Single(e=>e.OfficialId=="dudu");
        Assert.Equal(new[]{"00:00","04:00","08:00","12:00","16:00","18:00","21:00"},dudu.Times);Assert.False(dudu.SoundsEnabled);
        var service=new EventScheduleService(repo);var alerts=new List<EventAlert>();service.Alert+=alerts.Add;
        foreach(var hour in new[]{0,4,8,12,16,18,21}) {var start=Server(hour,0);service.Evaluate(start.AddMinutes(-5));Assert.Contains(alerts,a=>a.EventId==dudu.Id&&a.ExpiresUtc==start);}
        Assert.Equal(Server(0,0,16),service.Upcoming(Server(23,59)).First().StartUtc);
        Assert.Equal(dudu.Times,new EventRepository(root).Events.Single(e=>e.OfficialId=="dudu").Times);
    }

    // Keep these legacy scenarios focused on Dudu/KVM. Mare/grouping has its own suite.
    static EventRepository Repo(string? root=null)
    {
        var repository=new EventRepository(root??Path.Combine(Path.GetTempPath(), "Farmland-events-tests-" + Guid.NewGuid()));
        repository.Events.RemoveAll(e=>e.OfficialId=="mare-infernal"); repository.Events.Single(e=>e.OfficialId=="dudu").Times=new(){"00:30","05:30","10:30","15:30","20:30","22:30"}; return repository;
    }
    static DateTimeOffset Server(int hour, int minute, int day=15, int second=0) => new(2026,10,day,hour,minute,second,TimeSpan.FromHours(-3));
    [Theory]
    [InlineData(0,30)] [InlineData(5,30)] [InlineData(10,30)] [InlineData(15,30)] [InlineData(20,30)] [InlineData(22,30)]
    public void AllDuduSchedulesNotifyFiveMinutesBefore(int hour,int minute)
    {
        var repo=Repo();var engine=new EventScheduleService(repo);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        var start=Server(hour,minute);engine.Evaluate(start.AddMinutes(-5).AddSeconds(-1));Assert.Empty(alerts);
        engine.Evaluate(start.AddMinutes(-5));var alert=Assert.Single(alerts);Assert.Contains("Fantasminhas",alert.Title);Assert.Contains("Faltam 05:00",alert.Message);Assert.Equal(start,alert.ExpiresUtc);
        engine.Evaluate(start.AddMinutes(-5));Assert.Single(alerts);
    }
    [Theory] [InlineData(20)] [InlineData(22)]
    public void KvmAlertsAreForOpening(int hour)
    {
        var engine=new EventScheduleService(Repo());var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;engine.Evaluate(Server(hour,0).AddMinutes(-5));
        var alert=Assert.Single(alerts);Assert.Contains("abre",alert.Message);Assert.Contains(hour+":00",alert.Message);Assert.Contains("KVM",alert.Title);
        var occurrence=engine.Upcoming(Server(hour-1,0)).First(o=>o.Event.OfficialId=="kvm");Assert.Contains("Abertura",occurrence.Timing);
    }
    [Fact] public void TodayIsChronologicalAndNextDayIncludes0030()
    {
        var engine=new EventScheduleService(Repo());var rows=engine.Occurrences(Server(23,59),true);
        Assert.Equal(8,rows.Count);Assert.Equal(new[]{"00:30","05:30","10:30","15:30","20:00","20:30","22:00","22:30"},rows.Select(r=>r.Time));
        var next=engine.Upcoming(Server(23,59)).First();Assert.Equal(Server(0,30,16),next.StartUtc);Assert.Equal("00:31:00",next.Countdown);
    }
    [Fact] public void StartupWithinWindowReportsCorrectRemainingTimeOnceAcrossRestart()
    {
        var repo=Repo();var engine=new EventScheduleService(repo);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        engine.Evaluate(Server(10,27,second:12));Assert.Contains("Faltam 02:48",Assert.Single(alerts).Message);
        var reload=new EventScheduleService(Repo(repo.Store.Root));reload.Alert+=alerts.Add;reload.Evaluate(Server(10,28));Assert.Single(alerts);
        engine.Evaluate(Server(10,30));Assert.Single(alerts);
    }
    [Fact] public void SuspensionSkipsPastEventsAndCatchesUpOnlyClosestThreshold()
    {
        var repo=Repo();repo.Settings.AdvanceMinutes=new(){30,15,10,5,1};var engine=new EventScheduleService(repo);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        engine.Evaluate(Server(8,0));engine.Evaluate(Server(10,27));Assert.Single(alerts);Assert.Contains("03:00",alerts[0].Message);
        Assert.Equal(4,repo.History.Count);Assert.Single(repo.History,h=>h.Status=="Encaminhado ao Windows");
        engine.Evaluate(Server(10,29));Assert.Equal(2,alerts.Count);engine.Evaluate(Server(10,29,second:30));Assert.Equal(2,alerts.Count);
        engine.Evaluate(Server(23,59));Assert.Equal(2,alerts.Count);
    }
    [Fact] public void EachLeadCanNotifyOnceAndDailyScheduleRepeats()
    {
        var repo=Repo();repo.Settings.AdvanceMinutes=new(){10,5};var engine=new EventScheduleService(repo);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        engine.Evaluate(Server(10,20));engine.Evaluate(Server(10,25));engine.Evaluate(Server(10,25));Assert.Equal(2,alerts.Count);
        engine.Evaluate(Server(10,25,16));Assert.Equal(3,alerts.Count);
        engine.Evaluate(Server(10,25));Assert.Equal(3,alerts.Count); // clock rollback
    }
    [Fact] public void IndividualAndGlobalSwitchesAndSoundAreRespected()
    {
        var repo=Repo();var engine=new EventScheduleService(repo);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        repo.Settings.Enabled=false;engine.Evaluate(Server(10,25));Assert.Empty(alerts);
        repo.Settings.Enabled=true;repo.Events[0].AlertsEnabled=false;engine.Evaluate(Server(10,25));Assert.Empty(alerts);
        repo.Events[0].AlertsEnabled=true;repo.Events[0].Enabled=false;engine.Evaluate(Server(10,25));Assert.Empty(alerts);
        repo.Events[0].Enabled=true;repo.Settings.Sounds=false;engine.Evaluate(Server(10,25));Assert.False(Assert.Single(alerts).Sounds);
    }
    [Fact] public void CustomTimesZoneAndSpecificLeadsPersist()
    {
        var repo=Repo();repo.Settings.TimeZoneId="UTC";
        repo.Events.Add(new(){Id="custom",Name="Teste",Times=new(){"12:00"},UseGlobalAdvances=false,AdvanceMinutes=new(){1}});repo.Save();
        var restored=Repo(repo.Store.Root);var engine=new EventScheduleService(restored);var alerts=new List<EventAlert>();engine.Alert+=alerts.Add;
        var utc=new DateTimeOffset(2026,10,15,11,55,0,TimeSpan.Zero);engine.Evaluate(utc);Assert.Empty(alerts);engine.Evaluate(utc.AddMinutes(4));Assert.Contains("Teste",Assert.Single(alerts).Title);
        Assert.Equal("UTC",restored.Settings.TimeZoneId);Assert.True(EventScheduleService.TryZone("America/Sao_Paulo",out _));Assert.False(EventScheduleService.TryZone("Not/AZone",out _));
    }
    [Fact] public void OfficialUpdatePreservesUserPreferencesAndCustomizedTimes()
    {
        var repo=Repo();var dudu=repo.Events.First(e=>e.OfficialId=="dudu");dudu.Times=new(){"12:34"};dudu.TimesCustomized=true;dudu.Enabled=false;dudu.AlertsEnabled=false;dudu.OfficialRevision=0;
        var kvm=repo.Events.First(e=>e.OfficialId=="kvm");kvm.Times=new(){"12:35"};kvm.OfficialRevision=0;repo.UpdateOfficial();
        Assert.Equal("12:34",Assert.Single(dudu.Times));Assert.False(dudu.Enabled);Assert.False(dudu.AlertsEnabled);Assert.Equal(new[]{"20:00","22:00"},kvm.Times);
    }
    [Fact] public void ViewModelSavesCustomEventAndPreferencesAndRefreshesDashboard()
    {
        var repo=Repo();var time=Server(19,50);var vm=new EventViewModel(repo,()=>time);
        Assert.Contains("KVM",vm.NextName);Assert.Equal("00:10:00",vm.Countdown);Assert.Equal(8,vm.Today.Count);Assert.Contains("Dudu",vm.FollowingSummary);
        vm.NewCommand.Execute(null);vm.EditName="Customizado";vm.EditTimes="19:56, 23:15";vm.EditDescription="Descrição";vm.EditIcon="★";vm.SaveEventCommand.Execute(null);
        Assert.Contains("Customizado",vm.NextName);Assert.Equal(10,vm.Today.Count);vm.ShowCompact=false;vm.DuduAlerts=false;vm.Sounds=false;
        vm.AdvanceOptions.Single(a=>a.Minutes==10).Selected=true;vm.TimeZoneInput="UTC";vm.SaveZoneCommand.Execute(null);
        var reload=Repo(repo.Store.Root);Assert.False(reload.Settings.ShowCompact);Assert.False(reload.Settings.Sounds);Assert.False(reload.Events.Single(e=>e.OfficialId=="dudu").AlertsEnabled);Assert.Contains(10,reload.Settings.AdvanceMinutes);Assert.Contains(reload.Events,e=>e.Name=="Customizado");
        var count=repo.Events.Count;vm.EditTimes="25:00";vm.SaveEventCommand.Execute(null);Assert.Equal(count,repo.Events.Count);Assert.Contains("válidos",vm.Message);
    }
    [Fact] public void CorruptEventsRecoverBackupAndDoNotDuplicateOfficialRecords()
    {
        var repo=Repo();repo.Settings.Enabled=false;repo.Save();File.WriteAllText(Path.Combine(repo.Store.Root,"events.json"),"broken");
        var reload=Repo(repo.Store.Root);Assert.Equal(2,reload.Events.Count);Assert.False(reload.Settings.Enabled);Assert.NotEmpty(reload.Store.Warnings);
    }
    [Fact] public void NotificationHistoryRemovesOnlyOldOccurrencesWhenSaving()
    {
        var repo=Repo();repo.History.Add(new(){Key="old",OccurrenceUtc=Server(10,30).AddDays(-31)});repo.History.Add(new(){Key="recent",OccurrenceUtc=Server(10,30).AddDays(-1)});
        new EventScheduleService(repo).Evaluate(Server(10,25));Assert.DoesNotContain(repo.History,h=>h.Key=="old");Assert.Contains(repo.History,h=>h.Key=="recent");
    }
    [Fact] public void DaylightSavingSkipsInvalidWallTimeAndAmbiguousTimeOccursOnce()
    {
        var repo=Repo();repo.Settings.TimeZoneId="America/New_York";
        repo.Events.Clear();repo.Events.Add(new(){Id="dst",Name="Teste DST",Times=new(){"02:30"}});
        var engine=new EventScheduleService(repo);
        var spring=new DateTimeOffset(2026,3,8,0,0,0,TimeSpan.FromHours(-5));
        Assert.Empty(engine.Occurrences(spring,true));
        Assert.Equal(9,engine.Upcoming(spring).First().ServerTime.Day);
        repo.Events[0].Times=new(){"01:30"};var autumn=new DateTimeOffset(2026,11,1,0,0,0,TimeSpan.FromHours(-4));
        var occurrence=Assert.Single(engine.Occurrences(autumn,true));Assert.Equal(TimeSpan.FromHours(-4),occurrence.ServerTime.Offset);
    }
}
