using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RagnarokCompanion.ViewModels;
using RagnarokCompanion.Views;
using Forms = System.Windows.Forms;

namespace RagnarokCompanion.Services;

public sealed class WindowManagementService
{
    private readonly MainViewModel vm;
    private readonly Action exit;
    private readonly DispatcherTimer saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    public MainWindow Panel { get; }
    public bool IsCompact { get; private set; }
    private Rect fullBounds = new(80, 80, 1160, 820);
    private bool changing;
    private bool fitQueued;
    public bool Exiting { get; set; }
    public WindowManagementService(MainViewModel viewModel, Action quit)
    {
        vm = viewModel; exit = quit;
        Panel = new() { DataContext = vm };
        Panel.Closing += PanelClosing;
        Panel.LocationChanged += (_, _) => Remember(); Panel.SizeChanged += (_, e) => { Remember(); if (e.WidthChanged) QueueFit(); };
        Panel.CompactContent.AddHandler(System.Windows.Controls.Expander.ExpandedEvent, new RoutedEventHandler((_, _) => QueueFit()));
        Panel.CompactContent.AddHandler(System.Windows.Controls.Expander.CollapsedEvent, new RoutedEventHandler((_, _) => QueueFit()));
        vm.Spotlight.PropertyChanged += (_, e) => { if (vm.ActiveCompactTab == Models.CompactTab.Spotlight && e.PropertyName is "Summary" or "Status") QueueFit(); };
        vm.Items.PropertyChanged += (_, _) => { if (vm.ActiveCompactTab == Models.CompactTab.Items) QueueFit(); };
        vm.Catalog.CompactFavorites.CollectionChanged += (_, _) => { if (vm.ActiveCompactTab == Models.CompactTab.Mvps) QueueFit(); };
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.ActiveCompactTab) || (vm.ActiveCompactTab == Models.CompactTab.Mining && e.PropertyName == nameof(vm.MiningAvailable))) QueueFit(); };
        Panel.Loaded += (_, _) => EnsureVisible(Panel);
        Panel.DpiChanged += (_, _) => { EnsureVisible(Panel); QueueFit(); };
        vm.OpenPanel += ShowPanel; vm.OpenCompact += ShowCompact; vm.SettingsChanged += ApplySettings;
        saveDebounce.Tick += (_, _) => { saveDebounce.Stop(); try { vm.SaveLayout(); } catch (Exception ex) { vm.Tell("Erro ao salvar posição: " + ex.Message); } };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
    }
    public void ShowPanel()
    {
        if (IsCompact)
        {
            Remember(); changing = true; IsCompact = false;
            Panel.CompactContent.Visibility = Visibility.Collapsed; Panel.FullView.Visibility = Visibility.Visible;
            Panel.MinWidth = 940; Panel.MinHeight = 640;
            Panel.Left = fullBounds.Left; Panel.Top = fullBounds.Top; Panel.Width = fullBounds.Width; Panel.Height = fullBounds.Height;
            changing = false;
        }
        ApplySettings(); Panel.Show(); Panel.WindowState = WindowState.Normal; EnsureVisible(Panel); Panel.Activate();
    }
    private void PanelClosing(object? sender, CancelEventArgs e)
    {
        if (Exiting) return;
        e.Cancel = true;
        if (vm.Settings.CloseToTray) Panel.Hide(); else Panel.Dispatcher.BeginInvoke(exit);
    }
    public void ShowCompact()
    {
        if (!IsCompact)
        {
            if (Panel.WindowState == WindowState.Normal) fullBounds = new(Panel.Left, Panel.Top, Panel.Width, Panel.Height);
            changing = true; Panel.WindowState = WindowState.Normal; IsCompact = true;
            Panel.FullView.Visibility = Visibility.Collapsed; Panel.CompactContent.Visibility = Visibility.Visible;
            Panel.MinWidth = 300; Panel.MinHeight = 220;
            Panel.Width = Finite(vm.Layout.Width, 300, 1200, 320); Panel.Height = Finite(vm.Layout.Height, 220, 1400, 220);
            Panel.Left = Finite(vm.Layout.Left, -100000, 100000, 80); Panel.Top = Finite(vm.Layout.Top, -100000, 100000, 80);
            changing = false;
        }
        ApplySettings(); Panel.Show(); FitCompact(); EnsureVisible(Panel); Panel.WindowState = WindowState.Normal; Panel.Activate();
        if (vm.ActiveCompactTab == Models.CompactTab.Spotlight) _ = vm.Spotlight.LoadAsync();
    }
    private static double Finite(double x, double min, double max, double fallback) => double.IsFinite(x) ? Math.Clamp(x, min, max) : fallback;
    public void ApplySettings()
    {
        Panel.Topmost = IsCompact && vm.Settings.Topmost; Panel.Opacity = IsCompact ? vm.Settings.Opacity : 1;
        Panel.FontSize = IsCompact ? vm.Settings.FontSize : 14;
        vm.Refresh();
        QueueFit();
    }
    private void QueueFit()
    {
        if (!IsCompact || fitQueued || Exiting) return;
        fitQueued = true;
        Panel.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { fitQueued = false; FitCompact(); }));
    }
    public void FitCompact()
    {
        if (!IsCompact || Exiting) return;
        Panel.CompactContent.InvalidateMeasure(); Panel.UpdateLayout();
        Panel.CompactContent.Measure(new Size(Math.Max(278, Panel.Width - 2), double.PositiveInfinity));
        double height = Math.Max(Panel.MinHeight, Panel.CompactContent.DesiredSize.Height + 2);
        if (double.IsFinite(height) && Math.Abs(Panel.Height - height) > 1) Panel.Height = height;
        EnsureVisible(Panel);
    }
    private void Remember()
    {
        if (changing || Exiting || !IsCompact || Panel.WindowState != WindowState.Normal || !Panel.IsLoaded) return;
        vm.Layout.Left = Panel.Left; vm.Layout.Top = Panel.Top; vm.Layout.Width = Panel.Width; vm.Layout.Height = Panel.Height;
        saveDebounce.Stop(); saveDebounce.Start();
    }
    private void DisplaysChanged(object? sender, EventArgs e) => Panel.Dispatcher.BeginInvoke(() => EnsureVisible(Panel));
    public static void EnsureVisible(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var from = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var point = transform.Transform(new Point(window.Left, window.Top));
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.WorkingArea.Contains((int)point.X, (int)point.Y)) ?? Forms.Screen.PrimaryScreen!;
        var area = screen.WorkingArea;
        var origin = from.Transform(new Point(area.Left, area.Top));
        var size = from.Transform(new Point(area.Width, area.Height));
        window.Width = Math.Min(window.Width, size.X); window.Height = Math.Min(window.Height, size.Y);
        window.Left = Math.Clamp(window.Left, origin.X, Math.Max(origin.X, origin.X + size.X - window.Width));
        window.Top = Math.Clamp(window.Top, origin.Y, Math.Max(origin.Y, origin.Y + size.Y - window.Height));
    }
    public void Dispose()
    {
        saveDebounce.Stop(); Remember(); saveDebounce.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        Exiting = true; Panel.Close();
    }
}
