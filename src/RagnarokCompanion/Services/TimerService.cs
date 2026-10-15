using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;

// Pure UTC calculations: UI ticks never decrement stored counters.
public sealed class TimerService
{
    public Settings Settings { get; }
    public MiningState Mining { get; }
    public List<Mvp> Mvps { get; }
    public List<HistoryEntry> History { get; }
    public event Action<HistoryEntry>? Alert;
    public TimerService(Settings settings, MiningState mining, List<Mvp> mvps, List<HistoryEntry> history)
    { Settings = settings; Mining = mining; Mvps = mvps; History = history; }
    public MiningPhase Phase(DateTimeOffset now) => Mining.EnteredUtc == null || now >= Mining.ReadyUtc ? MiningPhase.Available : now < Mining.ExitUtc ? MiningPhase.Mining : MiningPhase.Cooldown;
    public TimeSpan Remaining(DateTimeOffset now) => Phase(now) switch
    { MiningPhase.Mining => Mining.ExitUtc!.Value - now, MiningPhase.Cooldown => Mining.ReadyUtc!.Value - now, _ => TimeSpan.Zero };
    public double Progress(DateTimeOffset now)
    {
        if (Phase(now) == MiningPhase.Available) return 100;
        var start = Phase(now) == MiningPhase.Mining ? Mining.EnteredUtc!.Value : Mining.ExitUtc!.Value;
        var end = Phase(now) == MiningPhase.Mining ? Mining.ExitUtc!.Value : Mining.ReadyUtc!.Value;
        return Math.Clamp((now - start).TotalSeconds / (end - start).TotalSeconds * 100, 0, 100);
    }
    public void StartMining(DateTimeOffset entered)
    {
        Mining.EnteredUtc = entered.ToUniversalTime();
        Mining.ExitUtc = Mining.EnteredUtc.Value.AddMinutes(Settings.MiningMinutes);
        Mining.ReadyUtc = Mining.ExitUtc.Value.AddMinutes(Settings.CooldownMinutes);
        Mining.Alerts.Clear();
        Record("Mineração", "Entrada na mina", entered);
    }
    public void ResetMining(DateTimeOffset now)
    {
        Mining.EnteredUtc = Mining.ExitUtc = Mining.ReadyUtc = null;
        Mining.Alerts.Clear(); Record("Mineração", "Ciclo reiniciado manualmente", now);
    }
    public void Kill(Mvp mvp, DateTimeOffset death)
    {
        mvp.DeathUtc = death.ToUniversalTime(); mvp.Alerts.Clear();
        Record("Morte", $"{mvp.Name} • {mvp.Map}", death);
    }
    public string Status(Mvp mvp, DateTimeOffset now) => mvp.DeathUtc == null ? "Sem registro" : now < mvp.StartUtc ? "Aguardando" : now < mvp.EndUtc ? "Janela aberta" : "Respawn possível";
    public DateTimeOffset Next(Mvp m, DateTimeOffset now) => m.StartUtc ?? DateTimeOffset.MaxValue;
    public IEnumerable<Mvp> Ordered(DateTimeOffset now) => Mvps.OrderBy(m => Next(m, now)).ThenBy(m => m.Name);
    public bool Evaluate(DateTimeOffset now)
    {
        bool changed = false;
        void Emit(HashSet<string> sent, string key, string text, bool enabled)
        {
            if (!sent.Add(key)) return;
            changed = true;
            var entry = Record("Alerta", text, now);
            if (enabled) Alert?.Invoke(entry);
        }
        if (Mining.EnteredUtc != null)
        {
            if (now >= Mining.ExitUtc) Emit(Mining.Alerts, "exit", "Mineração concluída • cooldown iniciado", Settings.MiningAlerts);
            if (now >= Mining.ReadyUtc) Emit(Mining.Alerts, "ready", "Mina disponível para uma nova entrada", Settings.MiningAlerts);
        }
        foreach (var m in Mvps.Where(x => x.DeathUtc != null))
        {
            var due = Advances(Settings.AdvanceMinutes).Where(minutes => now >= m.StartUtc!.Value.AddMinutes(-minutes)).OrderBy(minutes => minutes).ToList();
            foreach (var minutes in due)
            {
                // Only the most recent crossed advance is relevant after sleep/restart.
                string key = "pre" + minutes.ToString(CultureInfo.InvariantCulture);
                if (now < m.StartUtc && minutes == due[0])
                    Emit(m.Alerts, key, $"{m.Name}: {(m.MinMinutes == m.MaxMinutes ? "respawn previsto" : "início da janela")} em {Format(m.StartUtc!.Value - now)}", Settings.MvpAlerts);
                else if (m.Alerts.Add(key)) changed = true;
            }
            if (now >= m.StartUtc) Emit(m.Alerts, "start", m.MinMinutes == m.MaxMinutes ? $"{m.Name}: respawn previsto atingido" : $"{m.Name}: início da janela de respawn", Settings.MvpAlerts);
            if (m.MaxMinutes > m.MinMinutes && now >= m.EndUtc) Emit(m.Alerts, "end", $"{m.Name}: fim da janela de respawn", Settings.MvpAlerts);
        }
        return changed;
    }
    public HistoryEntry Record(string kind, string text, DateTimeOffset now)
    {
        var entry = new HistoryEntry { Kind = kind, Message = text, Utc = now.ToUniversalTime() };
        History.Insert(0, entry);
        if (History.Count > 5000) History.RemoveRange(5000, History.Count - 5000);
        return entry;
    }
    public static IEnumerable<double> Advances(string text) => text.Split(',').Select(s => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : -1).Where(n => double.IsFinite(n) && n > 0 && n <= 10080).Distinct();
    public static string Format(TimeSpan time) => $"{(int)Math.Ceiling(Math.Max(0, time.TotalSeconds)) / 60:00}:{(int)Math.Ceiling(Math.Max(0, time.TotalSeconds)) % 60:00}";
}
