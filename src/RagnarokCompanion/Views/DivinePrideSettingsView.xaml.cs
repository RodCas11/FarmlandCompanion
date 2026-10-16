using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Views;
public partial class DivinePrideSettingsView : UserControl
{
    public DivinePrideSettingsView() => InitializeComponent();
    private void SaveKey(object sender, RoutedEventArgs e)
    { try { if (DataContext is CatalogViewModel vm) vm.StoreKey(KeyInput.Password); } finally { KeyInput.Clear(); } }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { try { Process.Start(new ProcessStartInfo("https://www.divine-pride.net/tools/api-doc") { UseShellExecute = true }); } catch { } e.Handled = true; }
}
