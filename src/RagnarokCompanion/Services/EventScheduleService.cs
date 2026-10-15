using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;

// All occurrence/alert decisions use UTC instants derived from server wall time.
// The app's existing central tick calls this service; no extra countdown loop.
public sealed class EventScheduleService
{
    private readonly EventRepository repository;
    public event Action<EventAlert>? Alert;
    public EventScheduleService(EventRepository repository) => this.repository = repository;
    public static bool TryZone(string? id, out TimeZoneInfo zone)
    {
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(id ?? ""); return true; }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { zone = TimeZoneInfo.Utc; return false; }
    }
    public TimeZoneInfo Zone => TryZone(repository.Settings.TimeZoneId, out var zone) ? zone : SpotlightRotationService.ServerZone;
    public List<EventOccurrence> Occurrences(DateTimeOffset now, bool todayOnly = false)
    {
        var zone = Zone; var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var result = new List<EventOccurrence>();
        foreach (var item in repository.Events.Where(e => e.Enabled))
            for (int day = 0; day < (todayOnly ? 1 : 3); day++)
                foreach (var text in item.Times)
                {
                    if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) continue;
                    var wall = DateTime.SpecifyKind(today.AddDays(day).Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
                    if (zone.IsInvalidTime(wall)) continue;
                    var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);
                    var server = new DateTimeOffset(wall, offset);
                    result.Add(new(item, server.ToUniversalTime(), server, now));
                }
        return result.OrderBy(e => e.StartUtc).ThenBy(e => e.Event.Name, StringComparer.Ordinal).ToList();
    }
    public List<EventOccurrence> Upcoming(DateTimeOffset now) => Occurrences(now).Where(o => o.StartUtc > now).ToList();
    public void Evaluate(DateTimeOffset now)
    {
        if (!repository.Settings.Enabled) return;
        var records = repository.History.Select(h => h.Key).ToHashSet(StringComparer.Ordinal);
        var alerts = new List<EventAlert>(); bool changed = false;
        foreach (var occurrence in Upcoming(now).Where(o => o.Event.AlertsEnabled))
        {
            var leads = EventRepository.Advances(occurrence.Event.UseGlobalAdvances ? repository.Settings.AdvanceMinutes : occurrence.Event.AdvanceMinutes);
            var due = leads.Where(m => now >= occurrence.StartUtc.AddMinutes(-m)).OrderBy(m => m).ToList();
            if (due.Count == 0) continue;
            // Catch up with only the closest due threshold, never a backlog of old alerts.
            int closest = due[0];
            foreach (int lead in due)
            {
                string key = occurrence.Event.Id + ":" + occurrence.StartUtc.UtcTicks + ":" + lead;
                if (!records.Add(key)) continue;
                bool send = lead == closest;
                repository.History.Add(new() { Key=key, OccurrenceUtc=occurrence.StartUtc, RecordedUtc=now, Status=send ? "Encaminhado ao Windows" : "Antecedência ultrapassada" });
                changed = true;
                if (send)
                {
                    var remaining = occurrence.StartUtc - now;
                    string action = occurrence.Event.IsOpening ? "abre" : "começa";
                    string message = $"{occurrence.Event.Name} {action} às {occurrence.Time} (fuso {repository.Settings.TimeZoneId}). Faltam {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}. Prepare-se para o evento.";
                    alerts.Add(new(key, occurrence.Event.Id, occurrence.Title, message, repository.Settings.Sounds, occurrence.StartUtc));
                }
            }
        }
        if (!changed) return;
        repository.History.RemoveAll(h => h.OccurrenceUtc < now.AddDays(-30));
        // Persist before handing off; restart/clock rollback cannot replay an occurrence.
        repository.SaveHistory();
        foreach (var alert in alerts) Alert?.Invoke(alert);
    }
}
