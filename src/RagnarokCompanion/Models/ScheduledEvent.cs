using System;
using System.Collections.Generic;
namespace RagnarokCompanion.Models;

public sealed class ScheduledEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OfficialId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "🔔";
    public bool IsOpening { get; set; }
    public bool Enabled { get; set; } = true;
    public bool AlertsEnabled { get; set; } = true;
    public bool UseGlobalAdvances { get; set; } = true;
    public List<int> AdvanceMinutes { get; set; } = new() { 5 };
    public List<string> Times { get; set; } = new();
    public bool TimesCustomized { get; set; }
    public int OfficialRevision { get; set; } = 1;
    [System.Text.Json.Serialization.JsonIgnore] public string Origin => string.IsNullOrEmpty(OfficialId) ? "Personalizado" : "Oficial • administração Farmland";
}
public sealed class EventSettings
{
    public bool Enabled { get; set; } = true;
    public bool Sounds { get; set; } = true;
    public bool ShowCompact { get; set; } = true;
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public List<int> AdvanceMinutes { get; set; } = new() { 5 };
}
public sealed class EventNotificationRecord
{
    public string Key { get; set; } = "";
    public DateTimeOffset OccurrenceUtc { get; set; }
    public DateTimeOffset RecordedUtc { get; set; }
    public string Status { get; set; } = "";
}
public sealed record EventOccurrence(ScheduledEvent Event, DateTimeOffset StartUtc, DateTimeOffset ServerTime, DateTimeOffset Now)
{
    public string Title => Event.Icon + " " + Event.Name;
    public string Time => ServerTime.ToString("HH:mm");
    public string Timing => (Event.IsOpening ? "Abertura" : "Início") + " às " + Time;
    public string Countdown => StartUtc <= Now ? "Horário previsto passou" : TimerText(StartUtc - Now);
    public string State => StartUtc <= Now ? "Horário previsto passou" : "Programado";
    public string Origin => string.IsNullOrEmpty(Event.OfficialId) ? "Personalizado" : "Oficial • informado pela administração";
    private static string TimerText(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
}
public sealed record EventAlert(string Key, string EventId, string Title, string Message, bool Sounds, DateTimeOffset ExpiresUtc);
