using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;

namespace RagnarokCompanion;

public partial class App : Application
{
    private Mutex? mutex;
    private MainViewModel? vm;
    private WindowManagementService? windows;
    private NotificationService? notifications;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer settingsSave = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool quitting;
    private string smokeOutput = Path.Combine(Path.GetTempPath(), "RagnarokCompanion-validation");
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--check-ragnaplace"))
        {
            string? requested = e.Args.FirstOrDefault(a => a.StartsWith("--check-output=", StringComparison.Ordinal));
            string file = requested == null ? Path.Combine(Path.GetTempPath(), "RagnarokCompanion-validation", "ragnaplace-check.txt") : Path.GetFullPath(requested.Substring("--check-output=".Length));
            try { Directory.CreateDirectory(Path.GetDirectoryName(file)!); string result = await RagnaPlaceDiagnostics.CheckAsync(); File.WriteAllText(file, result); Shutdown(result.StartsWith("FAIL:") ? 1 : 0); }
            catch { Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--check-spotlight"))
        {
            string file = e.Args.FirstOrDefault(a => a.StartsWith("--check-output="))?.Substring("--check-output=".Length) ?? Path.Combine(Path.GetTempPath(), "spotlight-check.txt");
            try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!); File.WriteAllText(file, await SpotlightDiagnostics.CheckAsync()); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(file, "FAIL: " + ex); Shutdown(1); }
            return;
        }
        bool smoke = e.Args.Contains("--smoke-test");
        if (smoke)
        {
            string? output = e.Args.FirstOrDefault(a => a.StartsWith("--smoke-output=", StringComparison.Ordinal));
            if (output != null) smokeOutput = Path.GetFullPath(output.Substring("--smoke-output=".Length));
            Directory.CreateDirectory(smokeOutput);
        }
        mutex = new Mutex(true, smoke ? "Local\\RagnarokCompanion.Smoke" : "Local\\RagnarokCompanion", out bool first);
        if (!first) { MessageBox.Show("O Farmland Companion já está aberto. Procure o ícone na bandeja do Windows."); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true; vm?.Tell("Falha: " + args.Exception.Message);
            if (smoke) { File.AppendAllText(Path.Combine(smokeOutput, "smoke-errors.txt"), args.Exception + Environment.NewLine); File.WriteAllText(Path.Combine(smokeOutput, "smoke-test.txt"), "FAIL: " + args.Exception); Quit(1); }
            else MessageBox.Show(args.Exception.Message, "Farmland Companion");
        };
        try
        {
            var store = new PersistenceService(smoke ? Path.Combine(Path.GetTempPath(), "RagnarokCompanion-Smoke-" + Guid.NewGuid()) : null);
            if (smoke) store.Save("spotlight-catalog.json", SpotlightDiagnostics.Synthetic());
            vm = new(store);
            if (smoke) vm.Events.Enabled = false;
            windows = new(vm, Quit); MainWindow = windows.Panel;
            notifications = new(vm, windows.ShowPanel, windows.ShowCompact, Quit);
            settingsSave.Tick += (_, _) =>
            {
                settingsSave.Stop();
                try { PersistenceService.Validate(vm.Settings); windows.ApplySettings(); vm.Save(); }
                catch (Exception ex) { vm.Tell("Erro ao salvar preferências: " + ex.Message); }
            };
            vm.Settings.PropertyChanged += (_, _) => { settingsSave.Stop(); settingsSave.Start(); };
            windows.ShowPanel();
            if (vm.Settings.CompactOnStartup) windows.ShowCompact();
            timer.Tick += (_, _) => { try { vm.Tick(); } catch (Exception ex) { vm.Tell("Erro ao atualizar/salvar: " + ex.Message); } };
            vm.Tick(); timer.Start();
            if (smoke) _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(SmokeTest));
        }
        catch (Exception ex)
        {
            if (smoke) File.WriteAllText(Path.Combine(smokeOutput, "smoke-test.txt"), "FAIL: startup: " + ex);
            else MessageBox.Show("Não foi possível iniciar: " + ex.Message);
            Shutdown(1);
        }
    }
    private void SmokeTest()
    {
        string report = Path.Combine(smokeOutput, "smoke-test.txt");
        try
        {
            vm!.Events.Enabled = false;
            vm.Settings.CompactOnTimer = false;
            vm.Settings.Sounds = false;
            vm.EditName = "Freeoni"; vm.EditMap = "moc_fild17"; vm.EditMin = 60; vm.EditMax = 90; vm.SaveMvpCommand.Execute(null);
            vm.KillCommand.Execute(vm.Rows[0]);
            vm.NewMvpCommand.Execute(null);
            vm.EditName = "Orc Hero"; vm.EditMap = "gef_fild14"; vm.EditMin = vm.EditMax = 120; vm.SaveMvpCommand.Execute(null);
            vm.KillCommand.Execute(vm.Rows.First(r => r.Name == "Orc Hero"));
            if (vm.Engine.Mvps.Count != 2 || vm.NextRows.Count != 2) throw new Exception("Cadastro ou registro de morte falhou");
            vm.Search = "Freeoni"; if (vm.Rows.Count != 1) throw new Exception("Busca falhou"); vm.Search = "";
            vm.MapFilter = "gef_"; if (vm.Rows.Count != 1) throw new Exception("Filtro de mapa falhou"); vm.MapFilter = "";
            vm.Engine.StartMining(DateTimeOffset.UtcNow);
            vm.Refresh(); var originalWindow = windows!.Panel;
            double fullWidth = originalWindow.Width, fullHeight = originalWindow.Height;
            windows.ShowCompact();
            if (!ReferenceEquals(originalWindow, windows.Panel) || Windows.Count != 1 || !windows.IsCompact) throw new Exception("Modo compacto criou outra janela");
            if (windows.Panel.Topmost != vm.Settings.Topmost) throw new Exception("Topmost divergente");
            windows.Panel.Close();
            if (windows.Panel.IsVisible) throw new Exception("Fechar painel não ocultou para bandeja");
            var entered = vm.Engine.Mining.EnteredUtc;
            windows.ShowCompact(); windows.ShowPanel();
            if (windows.Panel.Width != fullWidth || windows.Panel.Height != fullHeight) throw new Exception("Tamanho do painel não restaurado");
            windows.ShowCompact();
            if (vm.Engine.Mining.EnteredUtc != entered) throw new Exception("Timer perdido ao reabrir");
            windows.Panel.Left = -50000; windows.Panel.Top = -50000; WindowManagementService.EnsureVisible(windows.Panel);
            if (windows.Panel.Left < SystemParameters.VirtualScreenLeft - 1) throw new Exception("Janela fora da tela");
            windows.ShowPanel();
            vm.Catalog.Gateways.Add(new Models.Gateway { Slug = "iro", Groups = new[] { "renewal" } }); vm.Catalog.GatewaySlug = "iro";
            vm.Spotlight.SelectedGroup = vm.Spotlight.Groups.Last();
            SpotlightDiagnostics.Verify(SpotlightDiagnostics.Synthetic());
            for (int p = 0; p < 6; p++)
            {
                vm.Page = p; windows.Panel.UpdateLayout();
                Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); windows.Panel.UpdateLayout();
                if ((p == 0 || p == 5) && !VisualTexts(windows.Panel).Contains(vm.Events.NextName)) throw new Exception("Card de eventos ausente");
                if (windows.Panel.Icon == null) throw new Exception("Ícone do aplicativo ausente");
                if (p == 4 && VisualElements<DataGrid>(windows.Panel).Single().Columns.Any(c => c.ActualWidth < 50)) throw new Exception("Colunas Spotlight ilegíveis");
                Capture(windows.Panel, Path.Combine(smokeOutput, $"smoke-page-{p}.png"));
            }
            vm.Catalog.SelectedEntry = vm.Catalog.Entries.First();
            vm.Catalog.RespawnMin = 60; vm.Catalog.RespawnMax = 90; vm.Catalog.ConfirmFarmland = true;
            vm.Catalog.SaveRespawnCommand.Execute(null); vm.Catalog.FavoriteCommand.Execute(null);
            if (vm.Catalog.FavoriteEntries.Count != 1) throw new Exception("Favorito não salvo");
            vm.Catalog.DeathTime = DateTime.Now.ToString("HH:mm");
            vm.Catalog.RegisterDeathCommand.Execute(null);
            if (vm.Engine.Mvps.Count != 3 || !vm.Engine.Mvps.Last().Origin.Contains("Farmland")) throw new Exception("Registro rápido de favorito offline falhou");
            foreach(var timer in vm.Engine.Mvps) { var entry=vm.Catalog.Entries.First(e=>e.Key==timer.CatalogKey);if(!entry.Favorite)vm.Catalog.ToggleFavoriteCommand.Execute(entry); }
            foreach(var entry in vm.Catalog.Entries.Where(e=>!e.Favorite).Take(2).ToArray())vm.Catalog.ToggleFavoriteCommand.Execute(entry);
            if(vm.Catalog.FavoriteEntries.Count!=5 || vm.Engine.Mvps.Count!=3)throw new Exception("Favoritar iniciou um timer");
            vm.Page=1;windows.Panel.UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);Capture(windows.Panel,Path.Combine(smokeOutput,"smoke-page-1.png"));
            windows.ShowCompact();
            vm.ActiveCompactTab = Models.CompactTab.Mvps;
            vm.Catalog.CustomCompactSelection=true;var fiveKeys=vm.Settings.CompactFavoriteKeys.ToArray();
            vm.Settings.CompactFavoriteKeys=fiveKeys.Take(1).ToList();vm.Catalog.RefreshFavoriteTimers();windows.FitCompact();double oneHeight=windows.Panel.Height;
            vm.Settings.CompactFavoriteKeys=fiveKeys.ToList();vm.Catalog.RefreshFavoriteTimers();windows.FitCompact();
            if(windows.Panel.Height<=oneHeight || vm.Catalog.CompactFavorites.Count!=5)throw new Exception("Altura/limite dos cinco favoritos incorreto");
            Capture(windows.Panel, Path.Combine(smokeOutput, "smoke-compact.png"));
            if(!VisualTexts(windows.Panel.CompactContent).Contains("Sem horário"))throw new Exception("Favorito sem morte não foi exibido");
            ValidateCompactTabs();
            if (!ReferenceEquals(originalWindow, windows.Panel) || Windows.Count != 1) throw new Exception("Identidade da janela alterada");
            vm.Save();
            vm.Engine.StartMining(DateTimeOffset.UtcNow.AddMinutes(-31));
            vm.Tick();
            if (notifications!.DeliveredCount != 1) throw new Exception("Notificação nativa não foi despachada");
            windows.Panel.Hide();
            vm.Events.Repository.Settings.Enabled = true;
            var nextEvent = vm.Events.Schedule.Upcoming(DateTimeOffset.UtcNow).First(e=>e.Event.OfficialId=="mare-infernal" && e.ServerTime.Hour is 0 or 10 or 20 or 22);
            var alertTime = nextEvent.StartUtc.AddMinutes(-5);
            int groupedMembers=0;
            void ObserveGroup(IReadOnlyList<Models.EventAlert> alerts) => groupedMembers=alerts.Count;
            vm.Events.Schedule.AlertBatch += ObserveGroup;
            vm.Events.Schedule.Evaluate(alertTime);
            windows.Panel.Show(); windows.Panel.WindowState = WindowState.Minimized;
            vm.Events.Schedule.Evaluate(alertTime);
            if (notifications.PendingCount != 1 || groupedMembers != 2) throw new Exception("Aviso simultâneo minimizado não foi agrupado em um único popup");
            vm.Events.Schedule.AlertBatch -= ObserveGroup;
            windows.Panel.WindowState = WindowState.Normal;
            vm.Events.Repository.Settings.Enabled = false;
            vm.ActiveCompactTab = Models.CompactTab.Events;
            windows.ShowCompact(); windows.FitCompact();
            if (!VisualTexts(windows.Panel.CompactContent).Contains(vm.Events.CompactUpcoming[0].Occurrence.Title) || !vm.Events.CompactUpcoming.Any(r=>r.Occurrence.Event.OfficialId=="mare-infernal")) throw new Exception("Eventos cronológicos ou Maré Infernal ausentes no compacto");
            if (Windows.Count != 1) throw new Exception("Eventos criou segunda janela");
            var restarted = new MainViewModel(new PersistenceService(vm.Store.Root));
            if (restarted.MiningStatus != "Cooldown" || restarted.Engine.Mvps.Count != 3) throw new Exception("Persistência de reinício divergiu");
            if (restarted.Catalog.FavoriteEntries.Count != 5) throw new Exception("Favoritos offline não restaurados");
            if (restarted.ActiveCompactTab != Models.CompactTab.Events) throw new Exception("Aba compacta não restaurada");
            restarted.Catalog.Dispose(); restarted.Spotlight.Dispose();
            int historyCount=vm.Engine.History.Count;vm.Catalog.ClearTimers();
            if(vm.Engine.Mvps.Any(m=>m.DeathUtc!=null)||vm.Catalog.FavoriteEntries.Count!=5||vm.Engine.History.Count!=historyCount)throw new Exception("Limpar timers apagou favoritos ou histórico");
            File.WriteAllText(report, "PASS: WPF startup; six navigation pages; event schedule/dashboard/compact/icon; minimized Dudu/Mare grouped popup queued once; chronological compact events; Spotlight search/filter/sort/offline/rotation; MVP create/kill; name search; map filter; exactly one MainWindow across modes; Topmost; close to tray; mode switching without losing timers; off-screen recovery; render six pages; compact auto height; four exclusive tabs; up to five favorite MVPs; HH:mm registration; clear timers preserves favorites/history; persistent tab; timers preserved across tabs; DPI renders 96/120/144/192; full size restoration; local catalog; Farmland respawn configuration; favorites offline; native notification dispatch; JSON save/reload; exit via panel with tray disabled.\nData: " + vm.Store.Root);
            vm.Settings.CloseToTray = false;
            windows.Panel.Close();
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); Quit(1); }
    }
    private void ValidateCompactTabs()
    {
        var panel=windows!.Panel;var model=vm!;var engine=model.Engine;var entered=engine.Mining.EnteredUtc;
        var deaths=engine.Mvps.Select(m=>m.DeathUtc).ToArray();
        var extra=new Models.Mvp{Name="Teste de rolagem",Map="test",DeathUtc=DateTimeOffset.UtcNow,MinMinutes=180,MaxMinutes=180};
        engine.Mvps.Add(extra);model.Refresh();
        foreach(var tab in Enum.GetValues<Models.CompactTab>())
        {
            model.ActiveCompactTab=tab;windows.FitCompact();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);windows.FitCompact();
            var buttons=VisualElements<RadioButton>(panel.CompactContent).Where(b=>b.GroupName=="CompactTabs").ToList();
            if(buttons.Count!=4 || buttons.Count(b=>b.IsChecked==true)!=1)throw new Exception("Barra de abas incompleta");
            foreach(var button in buttons)
            {
                var bounds=button.TransformToAncestor(panel.CompactContent).TransformBounds(new Rect(button.RenderSize));
                if(bounds.Left < -1 || bounds.Right > panel.CompactContent.ActualWidth+1 || bounds.Bottom > panel.CompactContent.ActualHeight+1)throw new Exception("Abas cortadas");
            }
            var texts=VisualTexts(panel.CompactContent).ToList();
            if(texts.Contains(model.MiningStatus)!=(tab==Models.CompactTab.Mining) || texts.Contains(model.Spotlight.Summary)!=(tab==Models.CompactTab.Spotlight) || texts.Contains(model.Events.CompactUpcoming[0].Occurrence.Title)!=(tab==Models.CompactTab.Events) || texts.Contains(model.Catalog.CompactFavorites[0].Name)!=(tab==Models.CompactTab.Mvps))throw new Exception("Conteúdo de módulos misturado");
            if(tab==Models.CompactTab.Mvps)
            {
                if(model.Catalog.CompactFavorites.Count!=5 || VisualElements<Button>(panel.CompactContent).Any(b=>b.Content?.ToString()=="Morreu"))throw new Exception("Compacto de favoritos contém controles extras");
            }
            foreach(double dpi in new[]{96d,120d,144d,192d})Capture(panel,Path.Combine(smokeOutput,$"compact-{tab}-{dpi}.png"),dpi);
            windows.ShowPanel();windows.ShowCompact();
            if(model.ActiveCompactTab!=tab || !ReferenceEquals(engine,model.Engine) || engine.Mining.EnteredUtc!=entered || !engine.Mvps.Take(3).Select(m=>m.DeathUtc).SequenceEqual(deaths) || Windows.Count!=1)throw new Exception("Troca de aba ou modo alterou timers/janela");
        }
        engine.Mvps.Remove(extra);model.Refresh();model.ActiveCompactTab=Models.CompactTab.Mvps;windows.FitCompact();
    }
    private static void Capture(Window window, string file, double dpi=96)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*dpi/96), (int)Math.Ceiling(window.ActualHeight*dpi/96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); png.Save(stream);
    }
    private static IEnumerable<string> VisualTexts(DependencyObject root)
    {
        if (root is TextBlock text) yield return text.Text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var value in VisualTexts(VisualTreeHelper.GetChild(root, i))) yield return value;
    }
    private static IEnumerable<T> VisualElements<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T element) yield return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisualElements<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private void Quit() => Quit(0);
    private void Quit(int code)
    {
        if (quitting) return; quitting = true; timer.Stop(); settingsSave.Stop();
        try { windows?.Dispose(); vm?.Save(); }
        catch (Exception ex) { if (code == 0) MessageBox.Show("Não foi possível salvar: " + ex.Message); }
        notifications?.Dispose(); vm?.Catalog.Dispose(); vm?.Spotlight.Dispose(); Shutdown(code);
    }
    protected override void OnExit(ExitEventArgs e) { notifications?.Dispose(); mutex?.Dispose(); base.OnExit(e); }
}
