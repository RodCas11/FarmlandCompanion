using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace RagnarokCompanion.Views;
public partial class CompactView : UserControl
{
    public CompactView() => InitializeComponent();
    private void DragHeader(object sender, MouseButtonEventArgs e) { if (e.OriginalSource is TextBlock or Image || ReferenceEquals(e.OriginalSource, sender)) Window.GetWindow(this)?.DragMove(); }
    private void Minimize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is { } w) w.WindowState = WindowState.Minimized; }
    private void CloseAppWindow(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
