using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
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
    private readonly ToolStripMenuItem pin = new("Sempre no topo"), sounds = new("Sons");
    private readonly Queue<HistoryEntry> pending = new();
    private readonly System.Windows.Threading.DispatcherTimer delivery = new() { Interval = TimeSpan.FromSeconds(7) };
    private bool disposed;
    public int DeliveredCount { get; private set; }
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
        tray = new NotifyIcon { Text = "Farmland Companion", Icon = SystemIcons.Information, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => panel(); tray.BalloonTipClicked += (_, _) => panel();
        vm.Engine.Alert += Notify;
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
        pending.Enqueue(entry);
        if (!delivery.IsEnabled) { Deliver(); delivery.Start(); }
    }
    private void Deliver()
    {
        if (pending.Count == 0) { delivery.Stop(); return; }
        var entry = pending.Dequeue();
        tray.ShowBalloonTip(6000, "Farmland Companion", entry.Message, ToolTipIcon.None);
        DeliveredCount++;
        if (vm.Settings.Sounds) SystemSounds.Asterisk.Play();
    }
    public void Dispose() { if (disposed) return; disposed = true; delivery.Stop(); vm.Engine.Alert -= Notify; tray.Visible = false; tray.Dispose(); }
}
