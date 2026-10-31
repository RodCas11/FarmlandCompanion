using System.Windows;
using System.Windows.Controls;
namespace RagnarokCompanion.Views;
public partial class InstancesView:UserControl
{
    public static readonly DependencyProperty CardColumnsProperty=DependencyProperty.Register(nameof(CardColumns),typeof(int),typeof(InstancesView),new PropertyMetadata(3));
    public int CardColumns {get=>(int)GetValue(CardColumnsProperty);set=>SetValue(CardColumnsProperty,value);}
    public InstancesView()=>InitializeComponent();
    void CardsSizeChanged(object sender,SizeChangedEventArgs e)=>CardColumns=ActualWidth>=1040?4:ActualWidth<650?2:3;
}
