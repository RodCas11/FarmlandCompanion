using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using Xunit;

namespace RagnarokCompanion.Tests;
public class TimerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 23, 50, 0, TimeSpan.Zero);
    private static TimerService Engine() => new(new(), new(), new(), new());
    [Fact] public void MiningTransitionsAtExactUtcBoundaries()
    {
        var e = Engine(); Assert.Equal(MiningPhase.Available, e.Phase(Start)); e.StartMining(Start);
        Assert.Equal(MiningPhase.Mining, e.Phase(Start.AddMinutes(29.99)));
        Assert.Equal(MiningPhase.Cooldown, e.Phase(Start.AddMinutes(30)));
        Assert.Equal(TimeSpan.FromMinutes(60), e.Remaining(Start.AddMinutes(30)));
        Assert.Equal(MiningPhase.Available, e.Phase(Start.AddMinutes(90)));
        Assert.Equal(100, e.Progress(Start.AddMinutes(90)));
        e.Evaluate(Start.AddMinutes(100)); Assert.Equal(Start, e.Mining.EnteredUtc);
    }
    [Fact] public void ResumeAfterSleepAndDayChangeEmitsEachTransitionOnce()
    {
        var e = Engine(); e.StartMining(Start); int alerts = 0; e.Alert += _ => alerts++;
        Assert.True(e.Evaluate(Start.AddHours(4))); Assert.False(e.Evaluate(Start.AddHours(5)));
        Assert.Equal(2, alerts); Assert.Equal(MiningPhase.Available, e.Phase(Start.AddHours(4)));
    }
    [Fact] public void ConfiguredDurationsAndCorrection()
    {
        var e = Engine(); e.Settings.MiningMinutes = 12; e.Settings.CooldownMinutes = 24;
        e.StartMining(Start); Assert.Equal(Start.AddMinutes(36), e.Mining.ReadyUtc);
        e.StartMining(Start.AddMinutes(-10)); Assert.Equal(TimeSpan.FromMinutes(2), e.Remaining(Start));
        e.ResetMining(Start); Assert.Equal(MiningPhase.Available, e.Phase(Start));
    }
    [Theory] [InlineData(60,60)] [InlineData(60,120)] public void FixedAndVariableRespawns(double min, double max)
    {
        var e = Engine(); var m = new Mvp { Name = "Freeoni", Map = "map", MinMinutes = min, MaxMinutes = max }; e.Mvps.Add(m);
        e.Kill(m, Start); Assert.Equal(Start.AddMinutes(min), m.StartUtc); Assert.Equal(Start.AddMinutes(max), m.EndUtc);
        Assert.Equal("Aguardando", e.Status(m, Start));
        Assert.Equal(min == max ? "Respawn possível" : "Janela aberta", e.Status(m, Start.AddMinutes(min)));
        Assert.Equal("Respawn possível", e.Status(m, Start.AddMinutes(max)));
    }
    [Fact] public void MultipleMvpsAndSimultaneousEventsDoNotCollide()
    {
        var e = Engine(); e.StartMining(Start);
        for (int i = 0; i < 5; i++) { var m = new Mvp { Name = "MVP" + i, MinMinutes = 30, MaxMinutes = 30 }; e.Mvps.Add(m); e.Kill(m, Start); }
        int count = 0; e.Alert += _ => count++; e.Evaluate(Start.AddMinutes(30));
        Assert.Equal(6, count); e.Evaluate(Start.AddMinutes(30)); Assert.Equal(6, count);
    }
    [Fact] public void AdvanceAlertsAreDeduplicatedAndCorrectingDeathStartsNewCycle()
    {
        var e = Engine(); var m = new Mvp(); e.Mvps.Add(m); e.Kill(m, Start); int count = 0; e.Alert += _ => count++;
        e.Evaluate(Start.AddMinutes(50)); e.Evaluate(Start.AddMinutes(50)); Assert.Equal(1, count);
        e.Evaluate(Start.AddMinutes(55)); e.Evaluate(Start.AddMinutes(59)); Assert.Equal(3, count);
        e.Kill(m, Start.AddHours(1)); e.Evaluate(Start.AddMinutes(110)); Assert.Equal(4, count);
    }
    [Fact] public void DisabledNotificationsStillRecordAndConsumeEvents()
    {
        var e = Engine(); e.Settings.MiningAlerts = false; e.StartMining(Start); int count = 0; e.Alert += _ => count++;
        e.Evaluate(Start.AddMinutes(90)); Assert.Equal(0, count); Assert.Equal(3, e.History.Count);
        e.Settings.MiningAlerts = true; e.Evaluate(Start.AddMinutes(91)); Assert.Equal(0, count);
    }
    [Fact] public void LocalOffsetIsNormalizedToUtc()
    {
        var e = Engine(); e.StartMining(Start.ToOffset(TimeSpan.FromHours(-3)));
        Assert.Equal(TimeSpan.Zero, e.Mining.EnteredUtc!.Value.Offset); Assert.Equal(Start.AddMinutes(30), e.Mining.ExitUtc);
    }
    [Fact] public void BackwardClockDoesNotDuplicateAlerts()
    {
        var e = Engine(); e.StartMining(Start); int count = 0; e.Alert += _ => count++;
        e.Evaluate(Start.AddMinutes(35)); e.Evaluate(Start.AddMinutes(20)); e.Evaluate(Start.AddMinutes(35)); Assert.Equal(1, count);
    }
    [Fact] public void RestartRestoresAbsoluteTimesAndDeduplication()
    {
        string root = Path.Combine(Path.GetTempPath(), "rc-test-" + Guid.NewGuid());
        try
        {
            var store = new PersistenceService(root); var e = Engine(); e.StartMining(Start); e.Evaluate(Start.AddMinutes(35));
            var m = new Mvp { Name = "Orc Hero", Map = "gef_fild" }; e.Mvps.Add(m); e.Kill(m, Start); e.Evaluate(Start.AddMinutes(55));
            store.Save("mining.json", e.Mining); store.Save("mvps.json", e.Mvps);
            var restart = new TimerService(new(), store.Load<MiningState>("mining.json"), store.Load<List<Mvp>>("mvps.json"), new());
            Assert.Equal(MiningPhase.Cooldown, restart.Phase(Start.AddMinutes(40))); Assert.False(restart.Evaluate(Start.AddMinutes(55)));
            Assert.Equal(Start, restart.Mvps[0].DeathUtc);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void InvalidJsonRecoversBackupAndKeepsCorruptedCopy()
    {
        string root = Path.Combine(Path.GetTempPath(), "rc-test-" + Guid.NewGuid());
        try
        {
            var p = new PersistenceService(root); p.Save("settings.json", new Settings { MiningMinutes = 17 }); p.Save("settings.json", new Settings { MiningMinutes = 20 });
            File.WriteAllText(Path.Combine(root, "settings.json"), "broken");
            Assert.Equal(17, p.Load<Settings>("settings.json").MiningMinutes); Assert.Single(p.Warnings); Assert.Single(Directory.GetFiles(root, "*.invalid-*"));
            File.WriteAllText(Path.Combine(root, "mining.json"), "broken"); Assert.Null(p.Load<MiningState>("mining.json").EnteredUtc);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void DurationFormatHandlesLongTimers()
    { Assert.Equal("120:00", TimerService.Format(TimeSpan.FromHours(2))); Assert.Equal("00:00", TimerService.Format(TimeSpan.FromSeconds(-1))); Assert.Equal("00:01", TimerService.Format(TimeSpan.FromMilliseconds(500))); }
    [Fact] public void VariableWindowNotifiesBothBoundariesWithoutInventingExactRespawn()
    {
        var e = Engine(); var m = new Mvp { MinMinutes = 60, MaxMinutes = 90 }; e.Mvps.Add(m); e.Kill(m, Start);
        var messages = new List<string>(); e.Alert += h => messages.Add(h.Message);
        Assert.False(e.Evaluate(Start.AddMinutes(49))); e.Evaluate(Start.AddMinutes(60)); e.Evaluate(Start.AddMinutes(90));
        Assert.Equal(2, messages.Count); Assert.Contains("início", messages[0]); Assert.Contains("fim", messages[1]);
    }
    [Fact] public void ProgressUsesRealTimeWithinEachPhase()
    {
        var e = Engine(); e.StartMining(Start);
        Assert.Equal(50, e.Progress(Start.AddMinutes(15))); Assert.Equal(0, e.Progress(Start.AddMinutes(30)));
        Assert.Equal(50, e.Progress(Start.AddMinutes(60))); Assert.Equal(100, e.Progress(Start.AddMinutes(150)));
    }
    [Fact] public void SettingsAndMvpValidationRejectsUnsafeRanges()
    {
        var s = new Settings { MiningMinutes = double.NaN, CooldownMinutes = -1, FontSize = 99, Opacity = 0, CompactCount = 77, CompactMode = "invalid" };
        PersistenceService.Validate(s);
        Assert.Equal(30, s.MiningMinutes); Assert.Equal(60, s.CooldownMinutes); Assert.Equal(24, s.FontSize); Assert.Equal(.5, s.Opacity); Assert.Equal(3, s.CompactCount); Assert.Equal("Ambos", s.CompactMode);
        Assert.False(PersistenceService.Valid(new Mvp { Name = "X", Map = "map", MinMinutes = 90, MaxMinutes = 60 }));
        Assert.False(PersistenceService.Valid(new Mvp { Name = "X", Map = "map", DeathUtc = DateTimeOffset.MaxValue }));
    }
    [Fact] public void AdvanceParsingIgnoresInvalidAndRepeatedValues()
    { Assert.Equal(new double[] { 10, 5, 1 }, TimerService.Advances("10,5,1,5,abc,-2,NaN,Infinity,10081")); }
    [Fact] public void ResumeInsideAdvanceRangeSendsOnlyMostRecentThreshold()
    {
        var e = Engine(); var m = new Mvp { MinMinutes = 60, MaxMinutes = 90 }; e.Mvps.Add(m); e.Kill(m, Start);
        var alerts = new List<string>(); e.Alert += h => alerts.Add(h.Message);
        e.Evaluate(Start.AddMinutes(59)); e.Evaluate(Start.AddMinutes(59.5));
        Assert.Single(alerts); Assert.Contains("início da janela", alerts[0]); Assert.Equal(3, m.Alerts.Count);
    }
}
