using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;

public sealed class EventRepository
{
    public PersistenceService Store { get; }
    public List<ScheduledEvent> Events { get; }
    public EventSettings Settings { get; }
    public List<EventNotificationRecord> History { get; }
    public static readonly int[] AllowedAdvances = { 1, 5, 10, 15, 30 };
    public EventRepository(string? root = null)
    {
        Store = new PersistenceService(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FarmlandCompanion"));
        Events = Store.Load<List<ScheduledEvent>>("events.json");
        Events.RemoveAll(e => !Valid(e));
        Events = Events.GroupBy(e => e.Id).Select(g => g.First()).ToList();
        Settings = Store.Load<EventSettings>("event-settings.json");
        if (!EventScheduleService.TryZone(Settings.TimeZoneId, out _)) Settings.TimeZoneId = "America/Sao_Paulo";
        Settings.AdvanceMinutes = Advances(Settings.AdvanceMinutes);
        History = Store.Load<List<EventNotificationRecord>>("event-notification-history.json");
        History.RemoveAll(h => h == null || string.IsNullOrWhiteSpace(h.Key));
        foreach (var item in Events) item.AdvanceMinutes = Advances(item.AdvanceMinutes);
        UpdateOfficial();
        Save();
    }
    public static List<int> Advances(IEnumerable<int>? values) => (values ?? new[] { 5 }).Where(AllowedAdvances.Contains).Distinct().OrderByDescending(x => x).ToList();
    public static bool Valid(ScheduledEvent? e) => e != null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name) && e.Name.Length <= 120 && e.Description != null && e.Description.Length <= 4000 && e.Icon != null && e.Icon.Length <= 16 && e.Times != null && e.Times.Count is > 0 and <= 48 && e.Times.All(t => TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) && e.Times.Distinct().Count() == e.Times.Count;
    public void UpdateOfficial()
    {
        foreach (var seed in Defaults())
        {
            var existing = Events.FirstOrDefault(e => e.OfficialId == seed.OfficialId);
            if (existing == null) Events.Add(seed);
            else if (existing.OfficialRevision < seed.OfficialRevision)
            {
                if (!existing.TimesCustomized) existing.Times = seed.Times;
                existing.OfficialRevision = seed.OfficialRevision;
            }
            if (existing != null) { existing.Halloween=seed.Halloween; existing.FirstDate=seed.FirstDate; existing.LastDate=seed.LastDate; existing.Duration=seed.Duration; }
        }
    }
    public static List<ScheduledEvent> Defaults() => RegularDefaults().Concat(HalloweenDefaults()).ToList();
    static ScheduledEvent Seasonal(string id, string name, string icon, string duration, IEnumerable<string> times) => new() { Id="official-"+id, OfficialId=id, Name=name, Icon="🎃 "+icon, Halloween=true, FirstDate=new(2026,10,26), LastDate=new(2026,11,2), Duration=duration, Times=times.ToList(), UseGlobalAdvances=false, AdvanceMinutes=new(){5} };
    static IEnumerable<ScheduledEvent> HalloweenDefaults() => new[] {
        Seasonal("niflheim", "A Maldição de Niflheim", "👻", "Duração: até 30 minutos", new[]{"15:00","21:00"}),
        Seasonal("pumpkins", "Caça às Abóboras Amaldiçoadas", "", "Duração: 20 minutos", Enumerable.Range(0,24).Select(h=>$"{h:00}:05")),
        Seasonal("zombies", "Infecção Zumbi", "🧟", "Inscrições: 5 minutos antes da partida", new[]{"20:15","22:15"})
    };
    static List<ScheduledEvent> RegularDefaults() => new()
    {
        new() { Id="official-poring", OfficialId="poring", Name="Corrida Poring", Icon="🏁", Times=new(){"15:20","17:20","19:20","21:20"}, UseGlobalAdvances=false, AdvanceMinutes=new(){5} },
        new() { Id="official-dudu", OfficialId="dudu", OfficialRevision=2, Name="Fantasminhas Dudu", Icon="👻", Times=new(){"00:00","04:00","08:00","12:00","16:00","18:00","21:00"}, Description="Evento automático com criaturas especiais em um mapa sorteado pelo servidor no momento do evento. Acesse pelo Botão de Menu enquanto estiver ativo, derrote os Fantasminhas Dudu e colete recompensas. O aplicativo não sabe qual mapa será escolhido." },
        new() { Id="official-kvm", OfficialId="kvm", Name="KVM: Batalha de Midgard", Icon="⚔", IsOpening=true, Times=new(){"20:00","22:00"}, Description="Abertura das inscrições para a batalha PvP entre Time Vermelho e Time Azul. Vença com mais eliminações dentro do tempo da partida. Taxa de entrada: 50.000z. Acesso: Batalhas Campais no menu do personagem. O servidor faz anúncios globais. O horário informado é de abertura; não confirma o início do combate." },
        new() { Id="official-mare-infernal", OfficialId="mare-infernal", Name="Maré Infernal", Icon="🔥", Times=new(){"00:30","02:30","04:30","06:30","08:30","10:30","12:30","14:30","16:30","18:30","20:30","22:30"}, Description="Evento diário a cada duas horas, começando às 00:30. Horários informados pela administração Farmland. Prepare-se para o evento." }
    };
    public void SaveEvents() => Store.Save("events.json", Events);
    public void SaveSettings() => Store.Save("event-settings.json", Settings);
    public void SaveHistory() => Store.Save("event-notification-history.json", History);
    public void Save() { SaveEvents(); SaveSettings(); SaveHistory(); }
}
