using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
namespace RagnarokCompanion.Views;
public partial class CatalogView : UserControl
{
    public static readonly System.Windows.DependencyProperty CardColumnsProperty=System.Windows.DependencyProperty.Register(nameof(CardColumns),typeof(int),typeof(CatalogView),new System.Windows.PropertyMetadata(3));
    public int CardColumns {get=>(int)GetValue(CardColumnsProperty);set=>SetValue(CardColumnsProperty,value);}
    private void CardsSizeChanged(object sender,System.Windows.SizeChangedEventArgs e){CardColumns=e.NewSize.Width>=1040?4:3;}
    public CatalogView() { InitializeComponent(); Loaded += (_,_) => { if(DataContext is ViewModels.CatalogViewModel vm)vm.PrepareSimpleView(); }; }
    private void OpenAttribution(object sender, RequestNavigateEventArgs e)
    { try { Process.Start(new ProcessStartInfo("https://www.divine-pride.net") { UseShellExecute = true }); } catch { } e.Handled = true; }
}
