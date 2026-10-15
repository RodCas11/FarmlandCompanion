using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
namespace RagnarokCompanion.Views;
public partial class CatalogView : UserControl
{
    public CatalogView() { InitializeComponent(); Loaded += (_,_) => { if(DataContext is ViewModels.CatalogViewModel vm)vm.PrepareSimpleView(); }; }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { try { Process.Start(new ProcessStartInfo("https://ragnaplace.com") { UseShellExecute = true }); } catch { } e.Handled = true; }
}
