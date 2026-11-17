using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RagnarokCompanion.ViewModels;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.Views;
public static class ItemTooltip {
 public static readonly DependencyProperty ModelProperty=DependencyProperty.RegisterAttached("Model",typeof(IItemTooltipModel),typeof(ItemTooltip),new PropertyMetadata(null,Changed));
 static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(State),typeof(ItemTooltip));
 public static void SetModel(DependencyObject target,IItemTooltipModel value)=>target.SetValue(ModelProperty,value);
 public static IItemTooltipModel? GetModel(DependencyObject target)=>(IItemTooltipModel?)target.GetValue(ModelProperty);
 static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e){if(target is not FrameworkElement host)return;(host.GetValue(StateProperty) as State)?.Dispose();host.SetValue(StateProperty,e.NewValue is IItemTooltipModel model?new State(host,model):null);}
 internal static Task<Popup> PreviewAsync(FrameworkElement host)=>((State)host.GetValue(StateProperty)).OpenAsync();
 sealed class State:IDisposable {
  readonly FrameworkElement host;readonly IItemTooltipModel model;readonly DispatcherTimer show=new(){Interval=TimeSpan.FromMilliseconds(400)},hide=new(){Interval=TimeSpan.FromMilliseconds(250)},refresh=new(){Interval=TimeSpan.FromMilliseconds(50)};Popup? popup;Window? owner;int generation;bool subscribed;StackPanel? buildHeader,buildBody;TextBlock? buildSource;
  public State(FrameworkElement host,IItemTooltipModel model){this.host=host;this.model=model;host.MouseEnter+=Enter;host.MouseLeave+=Leave;host.Loaded+=Load;host.Unloaded+=Unload;host.IsVisibleChanged+=VisibleChanged;if(host.IsLoaded)Load(host,new RoutedEventArgs());show.Tick+=Show;hide.Tick+=Hide;refresh.Tick+=async(_,_)=>{refresh.Stop();if(popup?.IsOpen==true&&model is IBuildItemTooltipModel build)await RenderBuild(build);};}
  void Enter(object sender,System.Windows.Input.MouseEventArgs e){hide.Stop();if(popup?.IsOpen!=true)show.Start();}
  void Leave(object sender,System.Windows.Input.MouseEventArgs e){show.Stop();hide.Start();}
  async void Show(object? sender,EventArgs e){show.Stop();if(host.IsMouseOver)await OpenAsync();}
  void Hide(object? sender,EventArgs e){hide.Stop();if(!host.IsMouseOver&&popup?.Child?.IsMouseOver!=true)Close();}
  void Load(object sender,RoutedEventArgs e){if(!subscribed){model.PropertyChanged+=ModelChanged;subscribed=true;}}
  void Unload(object sender,RoutedEventArgs e){Close();if(subscribed){model.PropertyChanged-=ModelChanged;subscribed=false;}}
  void VisibleChanged(object sender,DependencyPropertyChangedEventArgs e){if(!host.IsVisible)Close();}
  void ModelChanged(object? sender,PropertyChangedEventArgs e){if(model is IBuildItemTooltipModel){if(popup?.IsOpen==true){refresh.Stop();refresh.Start();}return;}if(e.PropertyName==nameof(model.SelectedItem))Close();}
  void Deactivate(object? sender,EventArgs e)=>Close();
  void Close(){show.Stop();hide.Stop();refresh.Stop();generation++;if(popup!=null)popup.IsOpen=false;if(owner!=null){owner.Deactivated-=Deactivate;owner.LocationChanged-=Deactivate;owner=null;}}
  public async Task<Popup> OpenAsync(){if(model is IBuildItemTooltipModel build)return await OpenBuild(build);Close();int version=generation;int? id=model.SelectedItem?.Id;var body=new StackPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,10)};if(Uri.TryCreate(model.Icon,UriKind.Absolute,out var icon)){var image=new Image{Width=32,Height=32,Margin=new Thickness(0,0,10,0),Source=new BitmapImage(icon)};DockPanel.SetDock(image,Dock.Left);header.Children.Add(image);}header.Children.Add(new TextBlock{Text=model.ItemName,FontSize=15,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});body.Children.Add(header);
   var description=new RagnarokDescriptionView{RawText="Carregando descrição local…"};var scroll=new ScrollViewer{Content=description,MaxHeight=Math.Min(360,SystemParameters.WorkArea.Height*.6),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};body.Children.Add(scroll);var source=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(154,166,191)),FontSize=10,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};body.Children.Add(source);
   var surface=new Border{Child=body,Width=340,Padding=new Thickness(13),Background=new SolidColorBrush(Color.FromRgb(24,30,45)),BorderBrush=new SolidColorBrush(Color.FromRgb(73,81,106)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9)};System.Windows.Documents.TextElement.SetForeground(surface,new SolidColorBrush(Color.FromRgb(237,240,250)));
   var opened=new Popup{Child=surface,PlacementTarget=host,Placement=PlacementMode.Bottom,VerticalOffset=4,AllowsTransparency=true,StaysOpen=true,Focusable=false};popup=opened;surface.MouseEnter+=(_,_)=>hide.Stop();surface.MouseLeave+=(_,_)=>hide.Start();surface.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};owner=Window.GetWindow(host);if(owner!=null){owner.Deactivated+=Deactivate;owner.LocationChanged+=Deactivate;}if(id==null)return opened;opened.IsOpen=true;
   try{var data=await model.Descriptions.GetAsync(id.Value);if(version==generation&&opened.IsOpen){description.RawText=data.RawText;source.Text=data.Source;}}catch{if(version==generation&&opened.IsOpen){description.RawText="Não foi possível ler a descrição local.";source.Text="Nenhuma consulta à API foi realizada.";}}return opened;
  }
  static Brush Muted=>new SolidColorBrush(Color.FromRgb(154,166,191));
  static Brush Accent=>System.Windows.Application.Current?.TryFindResource("ThemeE9C77A") as Brush??new SolidColorBrush(Color.FromRgb(233,199,122));
  static DockPanel ItemLine(string title,ImageSource? image,bool strong=false)
  {
   var row=new DockPanel{Margin=new Thickness(0,2,0,3)};
   FrameworkElement icon=image!=null?new Image{Source=image,Width=strong?28:22,Height=strong?28:22,Stretch=Stretch.Uniform}:new TextBlock{Text="◇",FontSize=22,Foreground=Muted,Width=strong?28:22,TextAlignment=TextAlignment.Center};icon.Margin=new Thickness(0,0,9,0);DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);row.Children.Add(new TextBlock{Text=title,FontSize=strong?14:11,FontWeight=strong?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap});return row;
  }
  async Task<Popup> OpenBuild(IBuildItemTooltipModel build)
  {
   Close();buildHeader=new();buildBody=new();buildSource=new(){Foreground=Muted,FontSize=9,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,0)};
   var body=new StackPanel();body.Children.Add(buildHeader);body.Children.Add(new ScrollViewer{Content=buildBody,MaxHeight=Math.Max(140,Math.Min(420,SystemParameters.WorkArea.Height*.55)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false});body.Children.Add(buildSource);
   var surface=new Border{Child=body,Width=Math.Min(340,SystemParameters.WorkArea.Width-24),Padding=new Thickness(10),Background=new SolidColorBrush(Color.FromRgb(24,30,45)),BorderBrush=new SolidColorBrush(Color.FromRgb(73,81,106)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9)};System.Windows.Documents.TextElement.SetForeground(surface,new SolidColorBrush(Color.FromRgb(237,240,250)));
   var opened=new Popup{Child=surface,PlacementTarget=host,Placement=PlacementMode.Bottom,VerticalOffset=4,AllowsTransparency=true,StaysOpen=true,Focusable=false};popup=opened;
   surface.MouseEnter+=(_,_)=>hide.Stop();surface.MouseLeave+=(_,_)=>hide.Start();surface.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};owner=Window.GetWindow(host);if(owner!=null){owner.Deactivated+=Deactivate;owner.LocationChanged+=Deactivate;}
   if(build.TooltipData.ItemId==null)return opened;opened.IsOpen=true;await RenderBuild(build);return opened;
  }
  static async Task<ItemDescription> LocalDescription(IItemTooltipModel model,int id){try{return await model.Descriptions.GetAsync(id);}catch{return new("Descrição local indisponível para este item.","Catálogo local • nenhuma consulta à API");}}
  async Task RenderBuild(IBuildItemTooltipModel model)
  {
   var snapshot=model.TooltipData;int version=++generation;if(snapshot.ItemId==null){Close();return;}
   if(buildHeader==null||buildBody==null||buildSource==null)return;
   buildHeader.Children.Clear();buildHeader.Children.Add(ItemLine(snapshot.Title,snapshot.Image,true));
   var originalTask=LocalDescription(model,snapshot.ItemId.Value);
   var original=await originalTask;if(version!=generation||popup?.IsOpen!=true)return;
   buildBody.Children.Clear();
   void Section(string title){if(buildBody.Children.Count>0)buildBody.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromRgb(52,62,80)),Margin=new Thickness(0,6,0,6)});buildBody.Children.Add(new TextBlock{Text=title,Foreground=Accent,FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,3,0,3)});}
   if(snapshot.Cards.Count>0){Section("Cartas");foreach(var card in snapshot.Cards)buildBody.Children.Add(ItemLine(card.Name+(card.Count>1?" ×"+card.Count:""),card.Image));}
   if(snapshot.Enchants.Count>0){Section("Encantamentos");foreach(var component in snapshot.Enchants)buildBody.Children.Add(ItemLine(component.Name+(component.Count>1?" ×"+component.Count:""),component.Image));}
   if(snapshot.RandomOptions.Count>0){Section("Random Options");foreach(var option in snapshot.RandomOptions)buildBody.Children.Add(new TextBlock{Text="• "+System.Text.RegularExpressions.Regex.Replace(option, @"\^[0-9a-fA-F]{6}", ""),Foreground=Accent,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,2)});}
   Section("Descrição do equipamento");buildBody.Children.Add(new RagnarokDescriptionView{RawText=original.RawText});buildSource.Text=original.Source;
  }
  public void Dispose(){Unload(host,new RoutedEventArgs());host.MouseEnter-=Enter;host.MouseLeave-=Leave;host.Loaded-=Load;host.Unloaded-=Unload;host.IsVisibleChanged-=VisibleChanged;}
 }
}
