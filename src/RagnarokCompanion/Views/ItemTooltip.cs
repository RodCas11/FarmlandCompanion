using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Views;
public static class ItemTooltip {
 public static readonly DependencyProperty ModelProperty=DependencyProperty.RegisterAttached("Model",typeof(ItemFarmViewModel),typeof(ItemTooltip),new PropertyMetadata(null,Changed));
 static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(State),typeof(ItemTooltip));
 public static void SetModel(DependencyObject target,ItemFarmViewModel value)=>target.SetValue(ModelProperty,value);
 public static ItemFarmViewModel? GetModel(DependencyObject target)=>(ItemFarmViewModel?)target.GetValue(ModelProperty);
 static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e){if(target is not FrameworkElement host)return;(host.GetValue(StateProperty) as State)?.Dispose();host.SetValue(StateProperty,e.NewValue is ItemFarmViewModel model?new State(host,model):null);}
 internal static Task<Popup> PreviewAsync(FrameworkElement host)=>((State)host.GetValue(StateProperty)).OpenAsync();
 sealed class State:IDisposable {
  readonly FrameworkElement host;readonly ItemFarmViewModel model;readonly DispatcherTimer show=new(){Interval=TimeSpan.FromMilliseconds(400)},hide=new(){Interval=TimeSpan.FromMilliseconds(250)};Popup? popup;Window? owner;int generation;bool subscribed;
  public State(FrameworkElement host,ItemFarmViewModel model){this.host=host;this.model=model;host.MouseEnter+=Enter;host.MouseLeave+=Leave;host.Loaded+=Load;host.Unloaded+=Unload;host.IsVisibleChanged+=VisibleChanged;if(host.IsLoaded)Load(host,new RoutedEventArgs());show.Tick+=Show;hide.Tick+=Hide;}
  void Enter(object sender,System.Windows.Input.MouseEventArgs e){hide.Stop();if(popup?.IsOpen!=true)show.Start();}
  void Leave(object sender,System.Windows.Input.MouseEventArgs e){show.Stop();hide.Start();}
  async void Show(object? sender,EventArgs e){show.Stop();if(host.IsMouseOver)await OpenAsync();}
  void Hide(object? sender,EventArgs e){hide.Stop();if(!host.IsMouseOver&&popup?.Child?.IsMouseOver!=true)Close();}
  void Load(object sender,RoutedEventArgs e){if(!subscribed){model.PropertyChanged+=ModelChanged;subscribed=true;}}
  void Unload(object sender,RoutedEventArgs e){Close();if(subscribed){model.PropertyChanged-=ModelChanged;subscribed=false;}}
  void VisibleChanged(object sender,DependencyPropertyChangedEventArgs e){if(!host.IsVisible)Close();}
  void ModelChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(model.SelectedItem))Close();}
  void Deactivate(object? sender,EventArgs e)=>Close();
  void Close(){show.Stop();hide.Stop();generation++;if(popup!=null)popup.IsOpen=false;if(owner!=null){owner.Deactivated-=Deactivate;owner.LocationChanged-=Deactivate;owner=null;}}
  public async Task<Popup> OpenAsync(){Close();int version=generation;int? id=model.SelectedItem?.Id;var body=new StackPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,10)};if(Uri.TryCreate(model.Icon,UriKind.Absolute,out var icon)){var image=new Image{Width=32,Height=32,Margin=new Thickness(0,0,10,0),Source=new BitmapImage(icon)};DockPanel.SetDock(image,Dock.Left);header.Children.Add(image);}header.Children.Add(new TextBlock{Text=model.ItemName,FontSize=15,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});body.Children.Add(header);
   var description=new RagnarokDescriptionView{RawText="Carregando descrição local…"};var scroll=new ScrollViewer{Content=description,MaxHeight=Math.Min(360,SystemParameters.WorkArea.Height*.6),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};body.Children.Add(scroll);var source=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(154,166,191)),FontSize=10,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};body.Children.Add(source);
   var surface=new Border{Child=body,Width=340,Padding=new Thickness(13),Background=new SolidColorBrush(Color.FromRgb(24,30,45)),BorderBrush=new SolidColorBrush(Color.FromRgb(73,81,106)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9)};System.Windows.Documents.TextElement.SetForeground(surface,new SolidColorBrush(Color.FromRgb(237,240,250)));
   var opened=new Popup{Child=surface,PlacementTarget=host,Placement=PlacementMode.Bottom,VerticalOffset=4,AllowsTransparency=true,StaysOpen=true,Focusable=false};popup=opened;surface.MouseEnter+=(_,_)=>hide.Stop();surface.MouseLeave+=(_,_)=>hide.Start();surface.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};owner=Window.GetWindow(host);if(owner!=null){owner.Deactivated+=Deactivate;owner.LocationChanged+=Deactivate;}if(id==null)return opened;opened.IsOpen=true;
   try{var data=await model.Descriptions.GetAsync(id.Value);if(version==generation&&opened.IsOpen){description.RawText=data.RawText;source.Text=data.Source;}}catch{if(version==generation&&opened.IsOpen){description.RawText="Não foi possível ler a descrição local.";source.Text="Nenhuma consulta à API foi realizada.";}}return opened;
  }
  public void Dispose(){Unload(host,new RoutedEventArgs());host.MouseEnter-=Enter;host.MouseLeave-=Leave;host.Loaded-=Load;host.Unloaded-=Unload;host.IsVisibleChanged-=VisibleChanged;}
 }
}
