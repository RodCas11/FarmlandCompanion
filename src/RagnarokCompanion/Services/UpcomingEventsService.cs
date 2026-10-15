using System;
using System.Collections.Generic;
using System.Linq;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;
public sealed record UpcomingEvent(string Title, string Status, DateTimeOffset Utc, string Color)
{
    public string Time => Utc.ToLocalTime().ToString("HH:mm");
    public string Remaining => TimerService.Format(Utc - DateTimeOffset.UtcNow);
}
public static class UpcomingEventsService
{
    public static List<UpcomingEvent> Get(TimerService engine, DateTimeOffset now)
    {
        var events = new List<UpcomingEvent>();
        if (engine.Mining.EnteredUtc != null)
        {
            if (engine.Mining.ExitUtc > now) events.Add(new("Fim da mineração", "Início do cooldown", engine.Mining.ExitUtc.Value, "#70C7FF"));
            if (engine.Mining.ReadyUtc > now) events.Add(new("Mina disponível", "Nova entrada liberada", engine.Mining.ReadyUtc.Value, "#6CD9A5"));
        }
        foreach (var m in engine.Mvps.Where(m => m.DeathUtc != null))
        {
            if (m.StartUtc > now) events.Add(new(m.Name, m.StartUtc == m.EndUtc ? "Respawn previsto" : "Abre a janela de respawn", m.StartUtc!.Value, m.StartUtc <= now.AddMinutes(10) ? "#FFBD70" : "#ADABF4"));
            else if (m.EndUtc > now) events.Add(new(m.Name, "Janela aberta • encerra às " + m.EndUtc.Value.ToLocalTime().ToString("HH:mm"), m.EndUtc.Value, "#FF8797"));
        }
        return events.OrderBy(e => e.Utc).ThenBy(e => e.Title).Take(8).ToList();
    }
}
