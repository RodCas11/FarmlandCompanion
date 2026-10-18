using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class CompactTabConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is CompactTab tab && Enum.TryParse<CompactTab>(parameter?.ToString(), out var expected) && tab == expected;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed partial class MainViewModel
{
    public static readonly CompactTab[] VisibleCompactTabs = { CompactTab.Mining, CompactTab.Mvps, CompactTab.Events, CompactTab.Items, CompactTab.Quests };
    public ObservableCollection<MvpRow> CompactAllRows { get; } = new();
    public bool NoMonitoredMvps => CompactAllRows.Count == 0;
    public double CompactMvpViewport => Settings.CompactCount * 48;
    public CompactTab ActiveCompactTab
    {
        get => Settings.CompactTab;
        set
        {
            if(value==CompactTab.Spotlight)value=CompactTab.Mining;
            if (!Enum.IsDefined(value) || Settings.CompactTab == value) return;
            Settings.CompactTab = value;
            Store.Save("settings.json", Settings);

            if (value == CompactTab.Items) Items.Restore();
        }
    }
    public ICommand SelectCompactTabCommand { get; private set; } = null!;
    public bool MiningAttention => CompactAttentionService.Mining(Engine, DateTimeOffset.UtcNow);
    public bool MvpAttention => CompactAttentionService.Mvps(Engine, DateTimeOffset.UtcNow);
    public bool EventAttention => CompactAttentionService.Events(Events.Schedule, DateTimeOffset.UtcNow);
    public bool SpotlightAttention => Spotlight.Pending;
    private void InitializeCompactTabs()
    {
        if(Settings.CompactTab==CompactTab.Spotlight){Settings.CompactTab=CompactTab.Mining;Store.Save("settings.json",Settings);}
        SelectCompactTabCommand = Cmd(p => { if (Enum.TryParse<CompactTab>(p?.ToString(), out var tab)) ActiveCompactTab = tab; });
        Settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Settings.CompactTab)) Changed(nameof(ActiveCompactTab)); };
    }
    private void RefreshCompactTabs()
    {
        foreach (var name in new[] { nameof(MiningAttention), nameof(MvpAttention), nameof(EventAttention), nameof(SpotlightAttention), nameof(CompactMvpViewport), nameof(NoMonitoredMvps) }) Changed(name);
    }
}
