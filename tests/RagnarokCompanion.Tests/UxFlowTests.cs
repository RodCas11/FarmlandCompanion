using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class UxFlowTests
{
    [Fact] public void EventsMergeMiningAndMvpsInChronologicalOrder()
    {
        var now = new DateTimeOffset(2026, 10, 15, 23, 50, 0, TimeSpan.Zero);
        var engine = new TimerService(new(), new(), new(), new()); engine.StartMining(now);
        var m = new Mvp { Name = "Próximo", MinMinutes = 10, MaxMinutes = 20 }; engine.Mvps.Add(m); engine.Kill(m, now);
        var events = UpcomingEventsService.Get(engine, now);
        Assert.Equal(new[] { "Próximo", "Fim da mineração", "Mina disponível" }, events.Select(e => e.Title));
        Assert.True(events.Zip(events.Skip(1)).All(pair => pair.First.Utc <= pair.Second.Utc));
        var inWindow = UpcomingEventsService.Get(engine, now.AddMinutes(15)); Assert.Contains("Janela aberta", inWindow[0].Status); Assert.Equal(now.AddMinutes(20), inWindow[0].Utc);
        Assert.Empty(UpcomingEventsService.Get(engine, now.AddHours(3)));
    }
    [Fact] public void FavoritesSortFirstAndQuickDeathNeedsOnlyOneActionOnceConfigured()
    {
        WithVm(vm =>
        {
            vm.Settings.CompactOnTimer = false;
            var favorite = vm.Catalog.Entries.Last(); vm.Catalog.SelectedEntry = favorite;
            vm.Catalog.RespawnMin = 90; vm.Catalog.RespawnMax = 120; vm.Catalog.ConfirmFarmland = true;
            vm.Catalog.SaveRespawnCommand.Execute(null); vm.Catalog.FavoriteCommand.Execute(null);
            Assert.Equal(favorite.Key, vm.Catalog.Entries[0].Key);
            vm.Catalog.QuickDeathCommand.Execute(vm.Catalog.Entries[0]);
            var timer = Assert.Single(vm.Engine.Mvps); Assert.Equal(90, timer.MinMinutes); Assert.Contains("Farmland", timer.Origin); Assert.Equal(0, vm.Page);
            vm.Catalog.Query = favorite.Name; Assert.All(vm.Catalog.Entries, e => Assert.Contains(favorite.Name, e.Name));
        });
    }
    [Fact] public void MissingRespawnOpensOnlyRequiredSetupWithoutCreatingFakeTimer()
    {
        WithVm(vm =>
        {
            vm.Catalog.QuickDeathCommand.Execute(vm.Catalog.Entries[0]); Assert.Equal(1, vm.Page); Assert.True(vm.Catalog.RespawnExpanded); Assert.Empty(vm.Engine.Mvps);
            vm.Catalog.RespawnMin = 60; vm.Catalog.RespawnMax = 90; vm.Catalog.SaveAndDeathCommand.Execute(null); Assert.Single(vm.Engine.Mvps);
        });
    }
    [Fact] public void ManualDeathAndFavoriteConfigurationPersistAcrossTheSimplifiedFlow()
    {
        WithVm(vm =>
        {
            vm.Catalog.SelectedEntry = vm.Catalog.Entries.First(); vm.Catalog.RespawnMin = vm.Catalog.RespawnMax = 120;
            vm.Catalog.SaveRespawnCommand.Execute(null); vm.Catalog.FavoriteCommand.Execute(null);
            var death = DateTime.Now.AddMinutes(-20); vm.Catalog.ManualDeath = death.ToString("dd/MM/yyyy HH:mm:ss"); vm.Catalog.ManualDeathCommand.Execute(null);
            Assert.True((Assert.Single(vm.Engine.Mvps).DeathUtc!.Value - new DateTimeOffset(death)).Duration() < TimeSpan.FromSeconds(1));
            var restart = new MainViewModel(new PersistenceService(vm.Store.Root));
            try { Assert.Single(restart.Catalog.FavoriteEntries); Assert.Single(restart.Engine.Mvps); } finally { restart.Catalog.Dispose(); }
        });
    }
    [Fact] public void MiningAvailabilityAndStatusColorsMatchTheCurrentPhase()
    {
        WithVm(vm =>
        {
            Assert.True(vm.MiningAvailable); Assert.Equal("#6CD9A5", vm.MiningColor);
            vm.Engine.StartMining(DateTimeOffset.UtcNow); Assert.False(vm.MiningAvailable); Assert.Equal("#70C7FF", vm.MiningColor);
            vm.Engine.StartMining(DateTimeOffset.UtcNow.AddMinutes(-31)); Assert.Equal("#FFBD70", vm.MiningColor);
        });
    }
    private static void WithVm(Action<MainViewModel> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "rc-ux-" + Guid.NewGuid()); MainViewModel? vm = null;
        try { vm = new(new PersistenceService(root)); action(vm); } finally { vm?.Catalog.Dispose(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact] public void MvpStatusUsesBothTextAndColorAndCompactClockRemainsShort()
    {
        var engine = new TimerService(new(), new(), new(), new());
        var m = new Mvp { Name = "MVP", MinMinutes = 60, MaxMinutes = 90, DeathUtc = DateTimeOffset.UtcNow.AddMinutes(-65) };
        var row = new MvpRow(m, engine); Assert.Equal("Janela aberta", row.Status); Assert.Equal("#FF8797", row.StatusColor); Assert.Matches("^[0-9]+:[0-9]{2}$", row.CompactCountdown);
        m.DeathUtc = DateTimeOffset.UtcNow.AddMinutes(-55); Assert.Equal("Próximo de nascer", row.Status); Assert.Equal("#FFBD70", row.StatusColor);
    }
}
