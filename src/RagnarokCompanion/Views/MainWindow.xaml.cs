using System.Windows;
using System.Windows.Input;
namespace RagnarokCompanion.Views;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is System.Windows.Controls.TextBlock || ReferenceEquals(e.OriginalSource, sender)) { if (e.ClickCount == 2) Maximize(sender, e); else DragMove(); } }
    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseAppWindow(object sender, RoutedEventArgs e) => Close();
}
