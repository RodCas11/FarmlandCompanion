using System;
using System.Collections.Generic;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;

public sealed record EventPopup(string Title, string Message, bool Sounds);
public static class EventNotificationComposer
{
    // Recheck current preferences at delivery: a queued group may lose a member.
    public static EventPopup? Compose(IReadOnlyList<EventAlert> alerts, EventRepository repository, DateTimeOffset now)
    {
        if (!repository.Settings.Enabled) return null;
        var eligible = alerts.Where(a => a.ExpiresUtc > now).DistinctBy(a => a.EventId)
            .Select(a => (Alert:a, Event:repository.Events.FirstOrDefault(e => e.Id==a.EventId)))
            .Where(p => p.Event is { Enabled:true, AlertsEnabled:true }).ToList();
        if (eligible.Count==0) return null;
        var names=eligible.Select(p=>p.Event!.Name).ToList();
        string name=names.Count==1 ? names[0] : string.Join(", ",names.Take(names.Count-1))+" e "+names[^1];
        string action=eligible.All(p=>p.Event!.IsOpening) ? (names.Count==1?"abre":"abrem") : eligible.All(p=>!p.Event!.IsOpening) ? (names.Count==1?"começa":"começam") : "têm início ou abertura";
        var start=eligible[0].Alert.ExpiresUtc;
        int seconds=(int)Math.Ceiling((start-now).TotalSeconds);
        string remaining=seconds%60==0 ? $"{seconds/60} minutos" : $"{seconds/60} min {seconds%60:00}s";
        var zone=EventScheduleService.TryZone(repository.Settings.TimeZoneId,out var configured)?configured:SpotlightRotationService.ServerZone;
        string message=$"{name} {action} em {remaining}! Prepare-se para {(names.Count==1?"o evento":"os eventos")}. Horário: {TimeZoneInfo.ConvertTime(start,zone):HH:mm} (fuso {repository.Settings.TimeZoneId}). Faltam {seconds/60:00}:{seconds%60:00}.";
        return new(names.Count==1?eligible[0].Alert.Title:"🔔 Eventos simultâneos",message,repository.Settings.Sounds && eligible.Any(p=>p.Event!.SoundsEnabled));
    }
}
