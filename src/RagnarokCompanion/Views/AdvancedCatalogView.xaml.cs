using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
namespace RagnarokCompanion.Views;
public partial class AdvancedCatalogView : UserControl
{
    public AdvancedCatalogView() => InitializeComponent();
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { try { Process.Start(new ProcessStartInfo("https://ragnaplace.com") { UseShellExecute = true }); } catch { } e.Handled = true; }
}
