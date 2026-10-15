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
            if (smoke) { File.WriteAllText(Path.Combine(smokeOutput, "smoke-test.txt"), "FAIL: " + args.Exception); Quit(1); }
            else MessageBox.Show(args.Exception.Message, "Farmland Companion");
        };
        try
        {
            var store = new PersistenceService(smoke ? Path.Combine(Path.GetTempPath(), "RagnarokCompanion-Smoke-" + Guid.NewGuid()) : null);
            if (smoke) store.Save("spotlight-catalog.json", SpotlightDiagnostics.Synthetic());
            vm = new(store); windows = new(vm, Quit); MainWindow = windows.Panel;
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
            vm!.Settings.CompactOnTimer = false;
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
            for (int p = 0; p < 5; p++)
            {
                vm.Page = p; windows.Panel.UpdateLayout();
                Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); windows.Panel.UpdateLayout();
                if (p == 4 && VisualElements<DataGrid>(windows.Panel).Single().Columns.Any(c => c.ActualWidth < 50)) throw new Exception("Colunas Spotlight ilegíveis");
                Capture(windows.Panel, Path.Combine(smokeOutput, $"smoke-page-{p}.png"));
            }
            vm.Catalog.SelectedEntry = vm.Catalog.Entries.First();
            vm.Catalog.RespawnMin = 60; vm.Catalog.RespawnMax = 90; vm.Catalog.ConfirmFarmland = true;
            vm.Catalog.SaveRespawnCommand.Execute(null); vm.Catalog.FavoriteCommand.Execute(null);
            if (vm.Catalog.FavoriteEntries.Count != 1) throw new Exception("Favorito não salvo");
            vm.Catalog.FavoriteDeathCommand.Execute(null);
            if (vm.Engine.Mvps.Count != 3 || !vm.Engine.Mvps.Last().Origin.Contains("Farmland")) throw new Exception("Registro rápido de favorito offline falhou");
            windows.ShowCompact();
            vm.Settings.CompactCount = 1; vm.Refresh(); windows.FitCompact(); double oneHeight = windows.Panel.Height;
            vm.Settings.CompactCount = 3; vm.Refresh(); windows.FitCompact();
            if (windows.Panel.Height <= oneHeight) throw new Exception($"Altura do compacto não acompanhou a quantidade de timers: 1={oneHeight}, 3={windows.Panel.Height}, desired={windows.Panel.CompactContent.DesiredSize.Height}");
            Capture(windows.Panel, Path.Combine(smokeOutput, "smoke-compact.png"));
            var favorites = VisualElements<Expander>(windows.Panel.CompactContent).Single(e => e.Header?.ToString() == "Registrar favorito");
            favorites.IsExpanded = true; windows.FitCompact(); windows.Panel.UpdateLayout();
            if (!VisualTexts(windows.Panel.CompactContent).Contains(vm.Catalog.SelectedFavorite!.Summary)) throw new Exception("Nome do favorito não foi exibido no seletor");
            Capture(windows.Panel, Path.Combine(smokeOutput, "smoke-compact-favorites.png")); favorites.IsExpanded = false;
            if (!ReferenceEquals(originalWindow, windows.Panel) || Windows.Count != 1) throw new Exception("Identidade da janela alterada");
            vm.Save();
            vm.Engine.StartMining(DateTimeOffset.UtcNow.AddMinutes(-31));
            vm.Tick();
            if (notifications!.DeliveredCount != 1) throw new Exception("Notificação nativa não foi despachada");
            var restarted = new MainViewModel(new PersistenceService(vm.Store.Root));
            if (restarted.MiningStatus != "Cooldown" || restarted.Engine.Mvps.Count != 3) throw new Exception("Persistência de reinício divergiu");
            if (restarted.Catalog.FavoriteEntries.Count != 1) throw new Exception("Favorito offline não restaurado");
            restarted.Catalog.Dispose(); restarted.Spotlight.Dispose();
            File.WriteAllText(report, "PASS: WPF startup; five navigation pages; Spotlight search/filter/sort/offline/rotation; MVP create/kill; name search; map filter; exactly one MainWindow across modes; Topmost; close to tray; mode switching without losing timers; off-screen recovery; render five pages; compact auto height; full size restoration; local catalog; Farmland respawn configuration; favorites offline; native notification dispatch; JSON save/reload; exit via panel with tray disabled.\nData: " + vm.Store.Root);
            vm.Settings.CloseToTray = false;
            windows.Panel.Close();
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); Quit(1); }
    }
    private static void Capture(Window window, string file)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
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
