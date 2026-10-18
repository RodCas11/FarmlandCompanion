using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RagnarokCompanion.Models;

public enum MiningPhase { Available, Mining, Cooldown }
public enum CompactTab { Mining, Mvps, Events, Spotlight, Items }
public sealed class Settings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
    private double miningMinutes = 30, cooldownMinutes = 60, opacity = .95, fontSize = 14;
    private bool sounds = true, miningAlerts = true, mvpAlerts = true, closeToTray = true, compactOnStartup, compactOnTimer = true, topmost = true;
    private string advanceMinutes = "10,5,1", compactMode = "Ambos";
    private int compactCount = 3;
    private CompactTab compactTab = CompactTab.Mining;
    public CompactTab CompactTab { get => compactTab; set => Set(ref compactTab, value); }
    public bool ChooseCompactFavorites { get; set; }
    public List<string> CompactFavoriteKeys { get; set; } = new();
    public bool CompactMvpAbsoluteTime {get;set;}
    public double MiningMinutes { get => miningMinutes; set => Set(ref miningMinutes, value); }
    public double CooldownMinutes { get => cooldownMinutes; set => Set(ref cooldownMinutes, value); }
    public bool Sounds { get => sounds; set => Set(ref sounds, value); }
    public bool MiningAlerts { get => miningAlerts; set => Set(ref miningAlerts, value); }
    public bool MvpAlerts { get => mvpAlerts; set => Set(ref mvpAlerts, value); }
    public string AdvanceMinutes { get => advanceMinutes; set => Set(ref advanceMinutes, value); }
    public bool CloseToTray { get => closeToTray; set => Set(ref closeToTray, value); }
    public bool CompactOnStartup { get => compactOnStartup; set => Set(ref compactOnStartup, value); }
    public bool CompactOnTimer { get => compactOnTimer; set => Set(ref compactOnTimer, value); }
    public bool Topmost { get => topmost; set => Set(ref topmost, value); }
    public double Opacity { get => opacity; set => Set(ref opacity, value); }
    public double FontSize { get => fontSize; set => Set(ref fontSize, value); }
    public int CompactCount { get => compactCount; set => Set(ref compactCount, value); }
    public string CompactMode { get => compactMode; set => Set(ref compactMode, value); }
}
public sealed class MiningState
{
    public DateTimeOffset? EnteredUtc { get; set; }
    public DateTimeOffset? ExitUtc { get; set; }
    public DateTimeOffset? ReadyUtc { get; set; }
    public HashSet<string> Alerts { get; set; } = new();
}
public sealed class Mvp
{
    public string CatalogKey { get; set; } = "";
    public bool HasRespawn {get;set;}=true;
    public string Origin { get; set; } = "Personalizado pelo usuário";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Map { get; set; } = "";
    public double MinMinutes { get; set; } = 60;
    public double MaxMinutes { get; set; } = 60;
    public string Notes { get; set; } = "";
    public DateTimeOffset? DeathUtc { get; set; }
    public HashSet<string> Alerts { get; set; } = new();
    [JsonIgnore] public DateTimeOffset? StartUtc => HasRespawn?DeathUtc?.AddMinutes(MinMinutes):null;
    [JsonIgnore] public DateTimeOffset? EndUtc => HasRespawn?DeathUtc?.AddMinutes(MaxMinutes):null;
}
public sealed class HistoryEntry
{
    public DateTimeOffset Utc { get; set; } = DateTimeOffset.UtcNow;
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    [JsonIgnore] public string LocalTime => Utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
public sealed class WindowLayout
{
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 330;
}
