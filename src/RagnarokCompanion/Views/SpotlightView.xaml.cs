using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Views;
public partial class SpotlightView : UserControl
{
 public SpotlightView(){InitializeComponent();Loaded+=async(_,_)=>{if(DataContext is SpotlightViewModel vm)await vm.LoadAsync();};}
 void OpenSource(object sender,RequestNavigateEventArgs e){Process.Start(new ProcessStartInfo(Services.SpotlightCatalogService.SourceUrl){UseShellExecute=true});e.Handled=true;}
}
