using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace RagnarokCompanion.Views;
public partial class QuestDetailsView:UserControl {
 public QuestDetailsView(){InitializeComponent();}
 private void MaterialImageFailed(object sender,System.Windows.ExceptionRoutedEventArgs e){if(sender is Image image){image.Source=new BitmapImage(new Uri("pack://application:,,,/Assets/AppIcon.ico"));e.Handled=true;}}
}
