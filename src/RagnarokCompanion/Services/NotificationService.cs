using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Linq;
using System.Windows.Forms;
using RagnarokCompanion.Models;
using RagnarokCompanion.ViewModels;

namespace RagnarokCompanion.Services;

// Shell notification area balloons are native Windows notifications, without a service,
// network dependency or registration. Windows notification settings can suppress them.
public sealed class NotificationService : IDisposable
{
    private readonly NotifyIcon tray;
    private readonly MainViewModel vm;
    private readonly ToolStripMenuItem mine = new("Mina"), next = new("Próximo MVP");
    private readonly ToolStripMenuItem pin = new("Sempre no topo"), sounds = new("Sons da mina e MVPs");
    private sealed record Notification(string Title, string Message, DateTimeOffset? Expires, IReadOnlyList<EventAlert>? Events = null);
    private readonly Queue<Notification> pending = new();
    private readonly Icon appIcon;
    private readonly System.Windows.Threading.DispatcherTimer delivery = new() { Interval = TimeSpan.FromSeconds(7) };
    private bool disposed;
    public int DeliveredCount { get; private set; }
    public int PendingCount => pending.Count;
    public NotificationService(MainViewModel viewModel, Action panel, Action compact, Action exit)
    {
        vm = viewModel;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir painel completo", null, (_, _) => panel());
        menu.Items.Add("Abrir modo compacto", null, (_, _) => compact());
        pin.Click += (_, _) => { vm.PinCommand.Execute(null); Refresh(); };
        menu.Items.Add(pin); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(mine); menu.Items.Add(next);
        mine.Click += (_, _) => panel(); next.Click += (_, _) => panel();
        sounds.Click += (_, _) => { vm.Settings.Sounds = !vm.Settings.Sounds; vm.Save(); Refresh(); };
        menu.Items.Add(sounds); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Sair", null, (_, _) => exit());
        menu.Opening += (_, _) => Refresh();
        using (var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico")).Stream)
        using (var source = new Icon(stream)) appIcon = (Icon)source.Clone();
        tray = new NotifyIcon { Text = "Farmland Companion", Icon = appIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => panel(); tray.BalloonTipClicked += (_, _) => panel();
        vm.Engine.Alert += Notify;
        vm.Events.Schedule.AlertBatch += NotifyEvents;
        delivery.Tick += (_, _) => Deliver();
    }
    public void Refresh()
    {
        mine.Text = "Mina: " + vm.MiningStatus + " " + vm.MiningCountdown;
        var m = vm.Engine.Ordered(DateTimeOffset.UtcNow);
        string text = "Nenhum MVP monitorado";
        foreach (var item in m) if (item.DeathUtc != null) { text = item.Name + " • " + new MvpRow(item, vm.Engine).Countdown; break; }
        next.Text = text; pin.Checked = vm.Settings.Topmost; sounds.Checked = vm.Settings.Sounds;
    }
    private void Notify(HistoryEntry entry)
    {
        pending.Enqueue(new("Farmland Companion", entry.Message, null));
        if (!delivery.IsEnabled) { Deliver(); delivery.Start(); }
    }
    private void NotifyEvents(IReadOnlyList<EventAlert> alerts)
    {
        if (alerts.Count==0) return;
        pending.Enqueue(new("", "", alerts[0].ExpiresUtc, alerts));
        if (!delivery.IsEnabled) { Deliver(); delivery.Start(); }
    }
    private void Deliver()
    {
        if (pending.Count == 0) { delivery.Stop(); return; }
        var entry = pending.Dequeue();
        EventPopup? popup = entry.Events==null?null:EventNotificationComposer.Compose(entry.Events,vm.Events.Repository,DateTimeOffset.UtcNow);
        while (entry.Expires <= DateTimeOffset.UtcNow || (entry.Events!=null && popup==null))
        {
            if (pending.Count == 0) { delivery.Stop(); return; }
            entry = pending.Dequeue();
            popup = entry.Events==null?null:EventNotificationComposer.Compose(entry.Events,vm.Events.Repository,DateTimeOffset.UtcNow);
        }
        string message = entry.Message;
        if (entry.Expires is { } expires)
        {
            var remaining = expires - DateTimeOffset.UtcNow;
            message = System.Text.RegularExpressions.Regex.Replace(message, @"Faltam \d+:\d+", $"Faltam {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}");
        }
        tray.ShowBalloonTip(6000, popup?.Title ?? entry.Title, popup?.Message ?? message, ToolTipIcon.None);
        DeliveredCount++;
        if (popup?.Sounds ?? vm.Settings.Sounds) SystemSounds.Asterisk.Play();
    }
    public void Dispose() { if (disposed) return; disposed = true; delivery.Stop(); vm.Engine.Alert -= Notify; vm.Events.Schedule.AlertBatch -= NotifyEvents; tray.Visible = false; tray.Dispose(); appIcon.Dispose(); }
}
