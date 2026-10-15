using System;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class CompactAttentionService
{
    public static bool Mining(TimerService engine, DateTimeOffset now) => engine.Mining.EnteredUtc != null && (engine.Phase(now) == MiningPhase.Available || (engine.Phase(now) == MiningPhase.Cooldown && now < engine.Mining.ExitUtc!.Value.AddMinutes(5)));
    public static bool Mvps(TimerService engine, DateTimeOffset now) => engine.Mvps.Any(m => m.DeathUtc != null && m.StartUtc <= now.AddMinutes(5));
    public static bool Events(EventScheduleService schedule, DateTimeOffset now) => schedule.Upcoming(now).Any(e => e.StartUtc <= now.AddMinutes(5));
}
